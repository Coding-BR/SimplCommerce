using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(AppDbContext db, IConfiguration configuration, IHttpClientFactory httpFactory, IHostEnvironment environment, IntegrationSettingsStore integrations) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("providers")]
    [AllowAnonymous]
    public async Task<IActionResult> Providers(CancellationToken ct)
    {
        var settings = await integrations.GetRuntimeAsync(ct);
        var providers = new List<string>();
        if (ProviderConfigured("PayPal", settings)) providers.Add("PayPal");
        if (ProviderConfigured("MercadoPago", settings)) providers.Add("MercadoPago");
        return Ok(providers);
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create(CreatePaymentRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId)) return BadRequest(new { error = "Pedido inválido." });
        var order = await db.Orders.SingleOrDefaultAsync(item => item.Id == orderId && (item.UserId == UserId || User.IsInRole("Admin")), ct); if (order is null) return NotFound();
        if (order.Status is "Paid" or "Processing" or "Shipped" or "Delivered") return Conflict(new { error = "Este pedido já está pago." });
        if (order.Status == "Cancelled") return Conflict(new { error = "Um pedido cancelado não pode receber pagamento." });
        var provider = NormalizeProvider(request.Provider); if (provider is null || provider == "Manual") return BadRequest(new { error = "Provedor não suportado." });
        var integrationSettings = await integrations.GetRuntimeAsync(ct);
        if (!ProviderConfigured(provider, integrationSettings)) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "O provedor de pagamento está desabilitado ou ainda não foi configurado." });
        var callback = SafeFrontendUrl(request.ReturnUrl, "/checkout/return"); var cancel = SafeFrontendUrl(request.CancelUrl, "/cart");
        var external = provider switch { "PayPal" => await TryCreatePayPal(order, callback, cancel, integrationSettings.PayPal, ct), "MercadoPago" => await TryCreateMercadoPago(order, callback, cancel, integrationSettings.MercadoPago, ct), _ => null };
        if (external is null && environment.IsProduction())
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "O provedor não respondeu. Nenhum pagamento foi criado; tente novamente." });
        var paymentId = external?.PaymentId ?? $"local-{Guid.NewGuid():N}"; var transaction = new PaymentTransactionRecord { OrderId = order.Id, Provider = provider, ProviderPaymentId = paymentId, Status = "created", AmountCents = order.TotalCents }; db.PaymentTransactions.Add(transaction); order.PaymentProvider = provider; order.PaymentIntentId = paymentId; order.Status = "AwaitingPayment"; await db.SaveChangesAsync(ct);
        var approvalUrl = external?.ApprovalUrl; if (string.IsNullOrWhiteSpace(approvalUrl)) { var separator = callback.Contains('?') ? "&" : "?"; approvalUrl = $"{callback}{separator}orderId={order.Id}&paymentId={paymentId}"; }
        return Ok(new { paymentId, approvalUrl, status = "created", provider });
    }

    [HttpPost("capture/{orderId:guid}")]
    [Authorize]
    public async Task<IActionResult> Capture(Guid orderId, CapturePaymentRequest request, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(item => item.Id == orderId && (item.UserId == UserId || User.IsInRole("Admin")), ct); if (order is null) return NotFound(); if (order.Status == "Cancelled") return Conflict(new { success = false, error = "O pedido foi cancelado." }); var payment = await db.PaymentTransactions.Where(item => item.OrderId == orderId).OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct); if (payment is null) return BadRequest(new { error = "Pagamento não iniciado." });
        if (payment.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)) return Ok(new { success = true, transactionId = payment.ProviderPaymentId, status = "approved", orderId });
        if (payment.Provider == "MercadoPago") return BadRequest(new { success = false, error = "A confirmação do Mercado Pago será processada pelo webhook." });
        if (payment.Provider != "PayPal") return BadRequest(new { success = false, error = "Método de pagamento inválido." });
        if (payment.Provider == "PayPal" && !payment.ProviderPaymentId.StartsWith("local-", StringComparison.Ordinal))
        {
            var runtime = await integrations.GetRuntimeAsync(ct);
            var captured = await TryCapturePayPal(payment.ProviderPaymentId, runtime.PayPal, ct); if (!captured) return BadRequest(new { success = false, error = "PayPal ainda não confirmou a captura." });
        }
        await db.Entry(order).ReloadAsync(ct); if (order.Status == "Cancelled") return Conflict(new { success = false, error = "A reserva deste pedido expirou. Crie um novo pedido." }); await MarkPaidAsync(order, payment, ct); return Ok(new { success = true, transactionId = payment.ProviderPaymentId, status = "approved", orderId });
    }

    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string provider, JsonElement payload, CancellationToken ct)
    {
        var normalized = NormalizeProvider(provider); if (normalized is null) return BadRequest(new { error = "Provedor não suportado." });
        // Webhooks devem ser autenticados pelo adaptador do provedor antes de
        // aceitar qualquer alteração de pedido. O segredo compartilhado é
        // usado no ambiente local; em produção deve ser substituído pela
        // verificação oficial de assinatura do provedor.
        var runtime = await integrations.GetRuntimeAsync(ct);
        var webhookSecret = normalized == "PayPal" ? runtime.PayPal.WebhookSecret : runtime.MercadoPago.WebhookSecret;
        var receivedSecret = Request.Headers["x-idealcreative-webhook-secret"].FirstOrDefault();
        if (environment.IsProduction() && (string.IsNullOrWhiteSpace(webhookSecret) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(webhookSecret), System.Text.Encoding.UTF8.GetBytes(receivedSecret ?? string.Empty)))) return Unauthorized();
        var eventId = Request.Headers["x-event-id"].FirstOrDefault() ?? (payload.TryGetProperty("id", out var id) ? id.GetString() : null) ?? Guid.NewGuid().ToString("N");
        if (await db.PaymentTransactions.AnyAsync(item => item.Provider == normalized && item.ProviderPaymentId == eventId, ct)) return Ok(new { received = true, duplicate = true });
        if (!TryGetOrderId(payload, out var orderId)) return BadRequest(new { error = "Webhook sem referência do pedido." });
        var orderRow = await db.Orders.FindAsync([orderId], ct);
        if (orderRow is null) return NotFound(new { error = "Pedido não encontrado." });
        var transaction = new PaymentTransactionRecord { OrderId = orderId, Provider = normalized, ProviderPaymentId = eventId, Status = GetWebhookStatus(payload), RawPayload = payload.GetRawText(), AmountCents = 0 }; db.PaymentTransactions.Add(transaction);
        if (transaction.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)) { await db.Entry(orderRow).ReloadAsync(ct); if (orderRow.Status == "Cancelled") { transaction.Status = "late_approval"; transaction.RawPayload = payload.GetRawText(); await db.SaveChangesAsync(ct); return Conflict(new { received = true, error = "Pedido expirado antes da confirmação; pagamento requer análise manual." }); } await MarkPaidAsync(orderRow, transaction, ct); }
        await db.SaveChangesAsync(ct); return Ok(new { received = true });
    }

    private static string? NormalizeProvider(string? provider) => provider?.Trim().ToLowerInvariant() switch { "paypal" => "PayPal", "mercadopago" or "mercado_pago" or "mercado pago" => "MercadoPago", "manual" => "Manual", _ => null };
    private async Task<ExternalPayment?> TryCreatePayPal(OrderRecord order, string returnUrl, string cancelUrl, PayPalRuntimeSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.Secret)) return null;
        var baseUrl = settings.BaseUrl.TrimEnd('/'); var http = httpFactory.CreateClient(); var auth = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }) }; auth.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.Secret}"))); var authResponse = await http.SendAsync(auth, ct); if (!authResponse.IsSuccessStatusCode) return null; using var authDoc = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync(ct)); var access = authDoc.RootElement.GetProperty("access_token").GetString(); if (string.IsNullOrWhiteSpace(access)) return null;
        var payload = new { intent = "CAPTURE", purchase_units = new[] { new { custom_id = order.Id.ToString(), amount = new { currency_code = "BRL", value = (order.TotalCents / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) } } }, application_context = new { return_url = returnUrl, cancel_url = cancelUrl } }; var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders") { Content = JsonContent.Create(payload) }; request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access); var response = await http.SendAsync(request, ct); if (!response.IsSuccessStatusCode) return null; using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var id = doc.RootElement.GetProperty("id").GetString(); var link = doc.RootElement.GetProperty("links").EnumerateArray().FirstOrDefault(item => item.TryGetProperty("rel", out var rel) && rel.GetString() == "approve"); return new ExternalPayment(id ?? string.Empty, link.ValueKind == JsonValueKind.Undefined ? null : link.GetProperty("href").GetString());
    }
    private async Task<ExternalPayment?> TryCreateMercadoPago(OrderRecord order, string returnUrl, string cancelUrl, MercadoPagoRuntimeSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.AccessToken)) return null; var http = httpFactory.CreateClient(); var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences") { Content = JsonContent.Create(new { items = new[] { new { title = "Pedido IdealCreative", quantity = 1, currency_id = "BRL", unit_price = order.TotalCents / 100m } }, back_urls = new { success = returnUrl, failure = cancelUrl, pending = returnUrl }, auto_return = "approved", external_reference = order.Id.ToString() }) }; request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.AccessToken); var response = await http.SendAsync(request, ct); if (!response.IsSuccessStatusCode) return null; using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return new ExternalPayment(doc.RootElement.GetProperty("id").GetString() ?? string.Empty, doc.RootElement.TryGetProperty("init_point", out var link) ? link.GetString() : null);
    }
    private async Task<bool> TryCapturePayPal(string paymentId, PayPalRuntimeSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.Secret)) return false; var baseUrl = settings.BaseUrl.TrimEnd('/'); var http = httpFactory.CreateClient(); var auth = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }) }; auth.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.Secret}"))); var authResponse = await http.SendAsync(auth, ct); if (!authResponse.IsSuccessStatusCode) return false; using var authDoc = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync(ct)); var access = authDoc.RootElement.GetProperty("access_token").GetString(); var capture = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders/{paymentId}/capture") { Content = JsonContent.Create(new { }) }; capture.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access); var response = await http.SendAsync(capture, ct); if (!response.IsSuccessStatusCode) return false; using var captureDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return captureDoc.RootElement.TryGetProperty("status", out var status) && string.Equals(status.GetString(), "COMPLETED", StringComparison.OrdinalIgnoreCase);
    }
    private static bool TryGetOrderId(JsonElement payload, out Guid orderId)
    {
        foreach (var value in new[] { payload, TryGetProperty(payload, "resource"), TryGetProperty(payload, "data") })
        {
            foreach (var key in new[] { "orderId", "external_reference", "custom_id" })
            {
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var candidate) && Guid.TryParse(candidate.GetString(), out orderId)) return true;
            }
        }
        if (payload.TryGetProperty("purchase_units", out var units) && units.ValueKind == JsonValueKind.Array)
            foreach (var unit in units.EnumerateArray())
                if (unit.TryGetProperty("custom_id", out var customId) && Guid.TryParse(customId.GetString(), out orderId)) return true;
        orderId = Guid.Empty;
        return false;
    }

    private static string GetWebhookStatus(JsonElement payload)
    {
        var status = TryGetProperty(payload, "status");
        if (status.ValueKind != JsonValueKind.String) status = TryGetProperty(TryGetProperty(payload, "resource"), "status");
        var value = status.GetString();
        return string.Equals(value, "COMPLETED", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "approved", StringComparison.OrdinalIgnoreCase) ? "approved" : value ?? "received";
    }

    private static JsonElement TryGetProperty(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private sealed record ExternalPayment(string PaymentId, string? ApprovalUrl);
    private static bool ProviderConfigured(string provider, IntegrationRuntimeSettings settings) => provider switch
    {
        "PayPal" => settings.PayPal.Enabled && !string.IsNullOrWhiteSpace(settings.PayPal.ClientId) && !string.IsNullOrWhiteSpace(settings.PayPal.Secret),
        "MercadoPago" => settings.MercadoPago.Enabled && !string.IsNullOrWhiteSpace(settings.MercadoPago.AccessToken),
        _ => false
    };

    private string SafeFrontendUrl(string? requested, string fallbackPath)
    {
        var frontendUrl = (configuration["Frontend:PublicUrl"] ?? "http://localhost:5289").TrimEnd('/');
        var fallback = frontendUrl + fallbackPath;
        if (string.IsNullOrWhiteSpace(requested)) return fallback;
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var candidate) || !Uri.TryCreate(frontendUrl, UriKind.Absolute, out var trusted)) return fallback;
        return candidate.Scheme == trusted.Scheme && string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase) && candidate.Port == trusted.Port ? candidate.ToString().TrimEnd('/') : fallback;
    }

    private async Task MarkPaidAsync(OrderRecord order, PaymentTransactionRecord payment, CancellationToken ct)
    {
        if (!order.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            var items = JsonSerializer.Deserialize<List<CommerceController.StoredCartItem>>(order.ItemsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            foreach (var item in items)
                if (Guid.TryParse(item.ProductId, out var productId))
                {
                    var product = await db.Products.FindAsync([productId], ct);
                    if (product is not null) product.SalesCount += item.Quantity;
                }
            if (!string.IsNullOrWhiteSpace(order.CouponCode))
            {
                var coupon = await db.Coupons.FindAsync([order.CouponCode], ct);
                if (coupon is not null) coupon.CurrentUsesGlobal++;
            }
        }
        payment.Status = "approved";
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        order.Status = "Paid";
        order.TransactionId = payment.ProviderPaymentId;
        order.PaidAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
    public sealed record CreatePaymentRequest(string OrderId, string Provider, string? ReturnUrl, string? CancelUrl);
    public sealed record CapturePaymentRequest(string? PayerId, string? Token);
}
