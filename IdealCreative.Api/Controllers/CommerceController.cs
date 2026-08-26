using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CommerceController(AppDbContext db, IntegrationSettingsStore integrations) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("carts")]
    [Authorize]
    public async Task<IActionResult> GetCart(CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct);
        return Ok(ToCartResponse(cart));
    }

    [HttpPost("carts/items")]
    [Authorize]
    public async Task<IActionResult> AddItem(CartItemRequest request, CancellationToken ct)
    {
        var product = await FindProduct(request.ProductId, ct);
        if (product is null || !product.IsPublished) return NotFound(new { message = "Produto não encontrado." });
        var quantity = Math.Max(1, request.Quantity);
        if (!product.IsDigital && quantity > product.Stock) return BadRequest(new { message = "Estoque insuficiente." });
        var cart = await GetOrCreateCart(ct);
        var shipping = (await integrations.GetRuntimeAsync(ct)).Shipping;
        var items = ReadItems(cart);
        var existing = items.FirstOrDefault(item => item.ProductId == product.Id.ToString());
        var selectedService = product.IsDigital ? null : ResolveShippingService(request.SelectedShippingServiceId, shipping);
        if (existing is null) items.Add(new StoredCartItem(product.Id.ToString(), product.Name, product.CoverImageUrl, product.PriceCents / 100d, product.IsDigital ? 1 : quantity, product.IsDigital) { SelectedShippingServiceId = selectedService, SelectedShippingName = ShippingName(selectedService), SelectedShippingCompany = selectedService.HasValue ? "IdealCreative" : null, SelectedShippingPrice = product.IsDigital ? null : CalculateShippingPrice(quantity, selectedService, shipping), SelectedShippingDeliveryTime = DeliveryTime(selectedService, shipping), SelectedShippingDescription = ShippingDescription(selectedService, shipping) });
        else { existing.Quantity = product.IsDigital ? 1 : Math.Min(product.Stock, existing.Quantity + quantity); if (!product.IsDigital && selectedService.HasValue) { existing.SelectedShippingServiceId = selectedService; existing.SelectedShippingName = ShippingName(selectedService); existing.SelectedShippingCompany = "IdealCreative"; existing.SelectedShippingPrice = CalculateShippingPrice(existing.Quantity, selectedService, shipping); existing.SelectedShippingDeliveryTime = DeliveryTime(selectedService, shipping); existing.SelectedShippingDescription = ShippingDescription(selectedService, shipping); } }
        cart.ItemsJson = JsonSerializer.Serialize(items, JsonOptions); cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(ToCartResponse(cart));
    }

    [HttpPut("carts/items/{productId}")]
    [Authorize]
    public async Task<IActionResult> UpdateItem(string productId, [FromBody] int quantity, CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct); var items = ReadItems(cart);
        var item = items.FirstOrDefault(value => value.ProductId == productId);
        if (item is null) return NotFound();
        if (!Guid.TryParse(productId, out var productGuid)) return BadRequest(new { message = "Produto inválido." });
        var product = await db.Products.FindAsync([productGuid], ct);
        if (product is null || !product.IsPublished) return BadRequest(new { message = "Este produto não está mais disponível." });
        if (!product.IsDigital && quantity > product.Stock) return BadRequest(new { message = "Estoque insuficiente." });
        var shipping = (await integrations.GetRuntimeAsync(ct)).Shipping;
        if (quantity <= 0) items.Remove(item); else { item.Quantity = product.IsDigital ? 1 : quantity; if (!product.IsDigital) item.SelectedShippingPrice = CalculateShippingPrice(item.Quantity, item.SelectedShippingServiceId, shipping); }
        cart.ItemsJson = JsonSerializer.Serialize(items, JsonOptions); cart.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct);
        return Ok(ToCartResponse(cart));
    }

    [HttpDelete("carts/items/{productId}")]
    [Authorize]
    public async Task<IActionResult> RemoveItem(string productId, CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct); var items = ReadItems(cart); items.RemoveAll(item => item.ProductId == productId);
        cart.ItemsJson = JsonSerializer.Serialize(items, JsonOptions); cart.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Ok(ToCartResponse(cart));
    }

    [HttpDelete("carts")]
    [Authorize]
    public async Task<IActionResult> ClearCart(CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct); cart.ItemsJson = "[]"; cart.CouponCode = null; cart.DiscountCents = 0; await db.SaveChangesAsync(ct); return Ok(ToCartResponse(cart));
    }

    [HttpPut("carts/shipping-zip-code")]
    [Authorize]
    public async Task<IActionResult> SetZip([FromBody] string zipCode, CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct); cart.ShippingZipCode = zipCode?.Trim(); await db.SaveChangesAsync(ct); return Ok(ToCartResponse(cart));
    }

    [HttpPost("carts/apply-coupon")]
    [Authorize]
    public async Task<IActionResult> ApplyCoupon(CouponRequest request, CancellationToken ct)
    {
        var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(item => item.Code == request.Code.Trim().ToUpperInvariant(), ct);
        var cart = await GetOrCreateCart(ct); var subtotal = ReadItems(cart).Sum(item => item.Price * item.Quantity);
        if (coupon is null || !coupon.IsActive || (coupon.StartDate.HasValue && coupon.StartDate > DateTimeOffset.UtcNow) || (coupon.EndDate.HasValue && coupon.EndDate < DateTimeOffset.UtcNow) || subtotal * 100 < coupon.MinPurchaseCents)
            return BadRequest(new { message = "Cupom inválido ou não disponível." });
        if (coupon.MaxUsesGlobal.HasValue && coupon.CurrentUsesGlobal >= coupon.MaxUsesGlobal.Value)
            return BadRequest(new { message = "Este cupom atingiu o limite de utilizações." });
        if (coupon.MaxUsesPerUser.HasValue && await db.Orders.CountAsync(item => item.UserId == UserId && item.CouponCode == coupon.Code && item.Status != "Cancelled", ct) >= coupon.MaxUsesPerUser.Value)
            return BadRequest(new { message = "Você já atingiu o limite de utilizações deste cupom." });
        cart.CouponCode = coupon.Code; cart.DiscountCents = CalculateDiscount(coupon, (long)Math.Round(subtotal * 100)); await db.SaveChangesAsync(ct); return Ok(ToCartResponse(cart));
    }

    [HttpGet("coupons/public")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicCoupons(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return Ok(await db.Coupons.AsNoTracking()
            .Where(item => item.IsActive
                && (!item.StartDate.HasValue || item.StartDate <= now)
                && (!item.EndDate.HasValue || item.EndDate >= now)
                && (!item.MaxUsesGlobal.HasValue || item.CurrentUsesGlobal < item.MaxUsesGlobal.Value))
            .OrderBy(item => item.Code).Select(ToCoupon).ToListAsync(ct));
    }

    [HttpGet("coupons")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Coupons(CancellationToken ct) => Ok(await db.Coupons.AsNoTracking().OrderBy(item => item.Code).Select(ToCoupon).ToListAsync(ct));

    [HttpPost("coupons")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCoupon(CouponRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant(); if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { message = "Código obrigatório." });
        if (await db.Coupons.AnyAsync(item => item.Code == code, ct)) return Conflict(new { message = "Cupom já existe." });
        var validation = ValidateCoupon(request); if (validation is not null) return BadRequest(new { message = validation });
        var coupon = new CouponRecord { Code = code, DiscountType = request.DiscountType ?? "Percentage", Value = (decimal)request.Value, MinPurchaseCents = (long)Math.Round(request.MinPurchaseAmount * 100), StartDate = request.StartDate, EndDate = request.EndDate, IsActive = request.IsActive ?? true, MaxUsesGlobal = request.MaxUsesGlobal, MaxUsesPerUser = request.MaxUsesPerUser };
        db.Coupons.Add(coupon); await db.SaveChangesAsync(ct); return Created($"/api/coupons/{code}", coupon);
    }

    [HttpDelete("coupons/{code}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCoupon(string code, CancellationToken ct) { var coupon = await db.Coupons.FindAsync([code.ToUpperInvariant()], ct); if (coupon is null) return NotFound(); db.Coupons.Remove(coupon); await db.SaveChangesAsync(ct); return NoContent(); }

    [HttpGet("coupons/{code}")]
    [Authorize]
    public async Task<IActionResult> GetCoupon(string code, CancellationToken ct) { var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code.ToUpperInvariant(), ct); return coupon is null ? NotFound() : Ok(coupon); }

    [HttpPut("coupons/{code}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCoupon(string code, CouponRequest request, CancellationToken ct) { var coupon = await db.Coupons.FindAsync([code.ToUpperInvariant()], ct); if (coupon is null) return NotFound(); var validation = ValidateCoupon(request); if (validation is not null) return BadRequest(new { message = validation }); coupon.DiscountType = request.DiscountType ?? "Percentage"; coupon.Value = (decimal)request.Value; coupon.MinPurchaseCents = (long)Math.Round(request.MinPurchaseAmount * 100); coupon.StartDate = request.StartDate; coupon.EndDate = request.EndDate; coupon.IsActive = request.IsActive ?? coupon.IsActive; coupon.MaxUsesGlobal = request.MaxUsesGlobal; coupon.MaxUsesPerUser = request.MaxUsesPerUser; await db.SaveChangesAsync(ct); return Ok(ToCouponResponse(coupon)); }

    [HttpPost("orders")]
    [Authorize]
    public async Task<IActionResult> CreateOrder(OrderRequest request, CancellationToken ct)
    {
        var cart = await GetOrCreateCart(ct); var storedItems = ReadItems(cart); if (storedItems.Count == 0) return BadRequest(new { message = "O carrinho está vazio." });
        var shippingSettings = (await integrations.GetRuntimeAsync(ct)).Shipping;
        var items = new List<StoredCartItem>();
        foreach (var stored in storedItems)
        {
            if (!Guid.TryParse(stored.ProductId, out var productId)) return BadRequest(new { message = "Item inválido." });
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == productId, ct);
            if (product is null || !product.IsPublished) return BadRequest(new { message = "Um produto do carrinho não está disponível." });
            var quantity = product.IsDigital ? 1 : Math.Max(1, stored.Quantity);
            var selectedService = product.IsDigital ? null : ResolveShippingService(stored.SelectedShippingServiceId, shippingSettings);
            items.Add(new StoredCartItem(product.Id.ToString(), product.Name, product.CoverImageUrl, product.PriceCents / 100d, quantity, product.IsDigital) { SelectedShippingServiceId = selectedService, SelectedShippingName = ShippingName(selectedService), SelectedShippingCompany = selectedService.HasValue ? "IdealCreative" : null, SelectedShippingPrice = product.IsDigital ? null : CalculateShippingPrice(quantity, selectedService, shippingSettings), SelectedShippingDeliveryTime = DeliveryTime(selectedService, shippingSettings), SelectedShippingDescription = ShippingDescription(selectedService, shippingSettings) });
        }
        if (items.Any(item => !item.IsDigital && !item.SelectedShippingServiceId.HasValue))
            return BadRequest(new { message = "Selecione entrega ou retirada local para todos os produtos físicos." });

        var subtotal = items.Sum(item => (long)Math.Round(item.Price * item.Quantity * 100));
        CouponRecord? coupon = null; var discount = 0L;
        if (!string.IsNullOrWhiteSpace(cart.CouponCode))
        {
            coupon = await db.Coupons.FindAsync([cart.CouponCode], ct); var now = DateTimeOffset.UtcNow;
            if (coupon is null || !coupon.IsActive || (coupon.StartDate.HasValue && coupon.StartDate > now) || (coupon.EndDate.HasValue && coupon.EndDate < now) || subtotal < coupon.MinPurchaseCents || (coupon.MaxUsesGlobal.HasValue && coupon.CurrentUsesGlobal >= coupon.MaxUsesGlobal.Value) || (coupon.MaxUsesPerUser.HasValue && await db.Orders.CountAsync(item => item.UserId == UserId && item.CouponCode == coupon.Code && item.Status != "Cancelled", ct) >= coupon.MaxUsesPerUser.Value))
                return BadRequest(new { message = "O cupom não está mais disponível para este pedido." });
            discount = CalculateDiscount(coupon, subtotal);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var item in items.Where(item => !item.IsDigital))
        {
            var id = Guid.Parse(item.ProductId);
            var changed = await db.Products.Where(product => product.Id == id && product.IsPublished && product.Stock >= item.Quantity)
                .ExecuteUpdateAsync(update => update.SetProperty(product => product.Stock, product => product.Stock - item.Quantity), ct);
            if (changed == 0) { await transaction.RollbackAsync(ct); return BadRequest(new { message = $"Estoque insuficiente para {item.ProductTitle}." }); }
        }
        var shipping = items.Sum(item => (long)Math.Round((item.SelectedShippingPrice ?? 0) * 100));
        var pickupOnly = items.Any(item => !item.IsDigital) && items.Where(item => !item.IsDigital).All(item => item.SelectedShippingServiceId == 3);
        var order = new OrderRecord { UserId = UserId, ItemsJson = JsonSerializer.Serialize(items, JsonOptions), SubtotalCents = subtotal, DiscountCents = discount, ShippingCents = shipping, TotalCents = Math.Max(0, subtotal - discount) + shipping, CouponCode = coupon?.Code, PaymentMethod = request.PaymentMethod, ShippingAddress = pickupOnly ? $"Retirada local — {shippingSettings.PickupAddress}" : request.ShippingAddress, CustomerName = request.CustomerName, CustomerEmail = request.CustomerEmail, CustomerPhone = request.CustomerPhone, ZipCode = pickupOnly ? null : request.ZipCode };
        db.Orders.Add(order); cart.ItemsJson = "[]"; cart.CouponCode = null; cart.DiscountCents = 0; cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(ToOrderResponse(order));
    }

    [HttpGet("orders")]
    [Authorize]
    public async Task<IActionResult> MyOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.Orders.AsNoTracking().Where(item => item.UserId == UserId); var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return Ok(new { items = rows.Select(ToOrderResponse), pagination = new { currentPage = page, pageSize, totalItems = total, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) } }); }

    [HttpGet("orders/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct) { var item = await db.Orders.AsNoTracking().SingleOrDefaultAsync(order => order.Id == id && order.UserId == UserId, ct); return item is null ? NotFound() : Ok(ToOrderResponse(item)); }

    [HttpGet("orders/admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? status = null, CancellationToken ct = default) { page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.Orders.AsNoTracking(); var normalizedStatus = NormalizeStatus(status); if (normalizedStatus is not null) query = query.Where(item => item.Status == normalizedStatus); var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return Ok(new { items = rows.Select(ToOrderResponse), pagination = new { currentPage = page, pageSize, totalItems = total, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) } }); }

    [HttpPut("orders/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOrder(Guid id, StatusRequest request, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([id], ct); if (order is null) return NotFound();
        var current = NormalizeStatus(order.Status) ?? order.Status; var next = NormalizeStatus(request.Status);
        if (next is null) return BadRequest(new { message = "Status inválido." });
        if (!CanTransition(current, next)) return Conflict(new { message = $"Não é possível alterar o pedido de {current} para {next}." });
        if (current == next) return Ok(ToOrderResponse(order));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (next == "Cancelled") await RestoreStockAsync(order, ct);
        if (next == "Paid") await AccountPaidOrderAsync(order, ct);
        order.Status = next; await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(ToOrderResponse(order));
    }

    [HttpDelete("orders/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken ct) { var item = await db.Orders.FindAsync([id], ct); if (item is null) return NotFound(); var status = NormalizeStatus(item.Status) ?? item.Status; if (IsPaidStatus(status)) return Conflict(new { message = "Pedidos pagos devem ser mantidos no histórico. Cancele ou conclua o atendimento em vez de excluir." }); await using var transaction = await db.Database.BeginTransactionAsync(ct); if (status != "Cancelled") await RestoreStockAsync(item, ct); db.Orders.Remove(item); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return NoContent(); }

    [HttpGet("orders/check-ownership/{productId}")]
    [Authorize]
    public async Task<IActionResult> CheckOwnership(string productId, CancellationToken ct) { if (!Guid.TryParse(productId, out var id)) return Ok(false); var orders = await db.Orders.AsNoTracking().Where(order => order.UserId == UserId && (order.Status == "Paid" || order.Status == "Processing" || order.Status == "Shipped" || order.Status == "Delivered")).ToListAsync(ct); return Ok(orders.Any(order => ContainsProduct(order, id))); }

    [HttpPost("orders/admin-purchase")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminPurchase(AdminPurchaseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || request.Items.Count == 0) return BadRequest(new { error = "Cliente e itens são obrigatórios." });
        if (!await db.Users.AnyAsync(user => user.Id == request.UserId, ct)) return BadRequest(new { error = "Cliente não encontrado." });
        var items = new List<StoredCartItem>();
        foreach (var requested in request.Items) { if (!Guid.TryParse(requested.ProductId, out var id)) return BadRequest(new { error = "Produto inválido." }); var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct); if (product is null || !product.IsPublished) return NotFound(new { error = "Produto não encontrado ou indisponível." }); var quantity = product.IsDigital ? 1 : Math.Max(1, requested.Quantity); items.Add(new StoredCartItem(product.Id.ToString(), product.Name, product.CoverImageUrl, product.PriceCents / 100d, quantity, product.IsDigital)); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var item in items.Where(item => !item.IsDigital)) { var id = Guid.Parse(item.ProductId); var changed = await db.Products.Where(product => product.Id == id && product.Stock >= item.Quantity).ExecuteUpdateAsync(update => update.SetProperty(product => product.Stock, product => product.Stock - item.Quantity), ct); if (changed == 0) { await transaction.RollbackAsync(ct); return BadRequest(new { error = $"Estoque insuficiente para {item.ProductTitle}." }); } }
        foreach (var item in items) { var id = Guid.Parse(item.ProductId); await db.Products.Where(product => product.Id == id).ExecuteUpdateAsync(update => update.SetProperty(product => product.SalesCount, product => product.SalesCount + item.Quantity), ct); }
        var subtotal = items.Sum(item => (long)Math.Round(item.Price * item.Quantity * 100)); var order = new OrderRecord { UserId = request.UserId, ItemsJson = JsonSerializer.Serialize(items, JsonOptions), SubtotalCents = subtotal, TotalCents = subtotal, Status = "Paid", PaymentMethod = "Admin", PaymentProvider = "Admin", PaidAt = DateTimeOffset.UtcNow }; db.Orders.Add(order); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Ok(ToOrderResponse(order));
    }

    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(new { totalProducts = await db.Products.CountAsync(ct), totalOrders = await db.Orders.CountAsync(ct), totalUsers = await db.Users.CountAsync(ct), totalSales = await db.Orders.Where(item => item.Status == "Paid" || item.Status == "Processing" || item.Status == "Shipped" || item.Status == "Delivered").SumAsync(item => item.TotalCents, ct) / 100m });

    private async Task<CartRecord> GetOrCreateCart(CancellationToken ct) { var id = UserId; var cart = await db.Carts.FindAsync([id], ct); if (cart is not null) return cart; cart = new CartRecord { UserId = id }; db.Carts.Add(cart); await db.SaveChangesAsync(ct); return cart; }
    private async Task<Product?> FindProduct(string id, CancellationToken ct) => Guid.TryParse(id, out var guid) ? await db.Products.FindAsync([guid], ct) : await db.Products.SingleOrDefaultAsync(item => item.Slug == id, ct);
    private static List<StoredCartItem> ReadItems(CartRecord cart) => JsonSerializer.Deserialize<List<StoredCartItem>>(cart.ItemsJson, JsonOptions) ?? [];
    private static object ToCartResponse(CartRecord cart) { var items = ReadItems(cart); var subtotal = items.Sum(item => item.Price * item.Quantity); return new { id = cart.UserId, items, updatedAt = cart.UpdatedAt, couponCode = cart.CouponCode, discountAmount = cart.DiscountCents / 100d, shippingZipCode = cart.ShippingZipCode, subTotal = subtotal, total = Math.Max(0, subtotal - cart.DiscountCents / 100d) }; }
    private static object ToOrderResponse(OrderRecord order) => new { id = order.Id, userId = order.UserId, items = JsonSerializer.Deserialize<List<StoredCartItem>>(order.ItemsJson, JsonOptions) ?? [], subTotal = order.SubtotalCents / 100d, discountAmount = order.DiscountCents / 100d, shippingCost = order.ShippingCents / 100d, total = order.TotalCents / 100d, status = order.Status, couponCode = order.CouponCode, paymentMethod = order.PaymentMethod, paymentProvider = order.PaymentProvider, paymentIntentId = order.PaymentIntentId, transactionId = order.TransactionId, paidAt = order.PaidAt, shippingAddress = order.ShippingAddress, customerName = order.CustomerName, customerEmail = order.CustomerEmail, customerPhone = order.CustomerPhone, zipCode = order.ZipCode, createdAt = order.CreatedAt };
    private static long CalculateDiscount(CouponRecord coupon, long subtotal) => coupon.DiscountType.Equals("Fixed", StringComparison.OrdinalIgnoreCase) ? Math.Min(subtotal, (long)Math.Round(coupon.Value * 100)) : Math.Min(subtotal, (long)Math.Round(subtotal * (coupon.Value / 100m)));
    private static string? ValidateCoupon(CouponRequest request)
    {
        if (request.DiscountType is not ("Percentage" or "Fixed")) return "Tipo de desconto inválido.";
        if (request.Value <= 0 || (request.DiscountType == "Percentage" && request.Value > 100)) return "Informe um desconto maior que zero e, para percentual, no máximo 100%.";
        if (request.MinPurchaseAmount < 0 || request.MaxUsesGlobal is <= 0 || request.MaxUsesPerUser is <= 0) return "Os limites do cupom devem ser positivos.";
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate) return "A data inicial deve ser anterior à data final.";
        return null;
    }
    private static object ToCouponResponse(CouponRecord item) => new { code = item.Code, discountType = item.DiscountType, value = item.Value, minPurchaseAmount = item.MinPurchaseCents / 100m, startDate = item.StartDate, endDate = item.EndDate, maxUsesGlobal = item.MaxUsesGlobal, maxUsesPerUser = item.MaxUsesPerUser, currentUsesGlobal = item.CurrentUsesGlobal, isActive = item.IsActive, createdAt = item.CreatedAt };
    private static string? NormalizeStatus(string? status) => status?.Trim().ToLowerInvariant() switch { "pending" or "pendente" => "Pending", "awaitingpayment" or "awaiting_payment" or "aguardando_pagamento" => "AwaitingPayment", "paid" or "pago" => "Paid", "processing" or "em_producao" => "Processing", "shipped" or "enviado" => "Shipped", "delivered" or "completed" or "concluido" => "Delivered", "cancelled" or "canceled" or "cancelado" => "Cancelled", null or "" => null, _ => null };
    private static bool IsPaidStatus(string status) => status is "Paid" or "Processing" or "Shipped" or "Delivered";
    private static bool CanTransition(string current, string next) => current == next || current switch { "Pending" => next is "AwaitingPayment" or "Paid" or "Cancelled", "AwaitingPayment" => next is "Paid" or "Cancelled", "Paid" => next is "Processing" or "Shipped" or "Delivered", "Processing" => next is "Shipped" or "Delivered", "Shipped" => next == "Delivered", _ => false };
    private async Task RestoreStockAsync(OrderRecord order, CancellationToken ct)
    {
        foreach (var item in ReadOrderItems(order).Where(item => !item.IsDigital)) { var id = Guid.Parse(item.ProductId); await db.Products.Where(product => product.Id == id).ExecuteUpdateAsync(update => update.SetProperty(product => product.Stock, product => product.Stock + item.Quantity), ct); }
    }
    private async Task AccountPaidOrderAsync(OrderRecord order, CancellationToken ct)
    {
        foreach (var item in ReadOrderItems(order)) { var id = Guid.Parse(item.ProductId); await db.Products.Where(product => product.Id == id).ExecuteUpdateAsync(update => update.SetProperty(product => product.SalesCount, product => product.SalesCount + item.Quantity), ct); }
        if (!string.IsNullOrWhiteSpace(order.CouponCode)) await db.Coupons.Where(coupon => coupon.Code == order.CouponCode).ExecuteUpdateAsync(update => update.SetProperty(coupon => coupon.CurrentUsesGlobal, coupon => coupon.CurrentUsesGlobal + 1), ct);
        order.PaidAt = DateTimeOffset.UtcNow; order.PaymentProvider ??= "Admin";
    }
    private static List<StoredCartItem> ReadOrderItems(OrderRecord order) => JsonSerializer.Deserialize<List<StoredCartItem>>(order.ItemsJson, JsonOptions) ?? [];
    private static int? NormalizeShippingService(int? serviceId) => serviceId is 1 or 2 or 3 ? serviceId : null;
    private static int? ResolveShippingService(int? serviceId, ShippingRuntimeSettings settings)
    {
        var normalized = NormalizeShippingService(serviceId);
        if (normalized == 3) return settings.LocalPickupEnabled ? 3 : null;
        if (normalized is 1 or 2) return settings.Enabled ? normalized : settings.LocalPickupEnabled ? 3 : null;
        return !settings.Enabled && settings.LocalPickupEnabled ? 3 : null;
    }
    private static string? ShippingName(int? serviceId) => serviceId switch { 1 => "Entrega econômica", 2 => "Entrega expressa", 3 => "Retirada local", _ => null };
    private static double? CalculateShippingPrice(int quantity, int? serviceId, ShippingRuntimeSettings settings) { var normalized = NormalizeShippingService(serviceId); if (normalized == 3) return settings.LocalPickupEnabled ? 0d : null; if (!normalized.HasValue || !settings.Enabled) return null; var economical = (double)settings.BasePrice + Math.Min((double)settings.MaxAdditionalPrice, Math.Max(1, quantity) * (double)settings.PricePerKg); return normalized == 2 ? economical + (double)settings.ExpressAdditionalPrice : economical; }
    private static int? DeliveryTime(int? serviceId, ShippingRuntimeSettings settings) => serviceId switch { 1 => settings.EconomyDeliveryDays, 2 => settings.ExpressDeliveryDays, 3 => settings.PickupPreparationDays, _ => null };
    private static string? ShippingDescription(int? serviceId, ShippingRuntimeSettings settings) => serviceId == 3 ? string.Join(" · ", new[] { settings.PickupAddress, settings.PickupInstructions }.Where(value => !string.IsNullOrWhiteSpace(value))) : null;
    private static bool ContainsProduct(OrderRecord order, Guid productId) => (JsonSerializer.Deserialize<List<StoredCartItem>>(order.ItemsJson, JsonOptions) ?? []).Any(item => item.ProductId == productId.ToString());
    private static readonly System.Linq.Expressions.Expression<Func<CouponRecord, object>> ToCoupon = item => new { code = item.Code, discountType = item.DiscountType, value = item.Value, minPurchaseAmount = item.MinPurchaseCents / 100m, startDate = item.StartDate, endDate = item.EndDate, maxUsesGlobal = item.MaxUsesGlobal, maxUsesPerUser = item.MaxUsesPerUser, currentUsesGlobal = item.CurrentUsesGlobal, isActive = item.IsActive, createdAt = item.CreatedAt };

    public sealed class StoredCartItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductTitle { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public bool IsDigital { get; set; }
        public int? SelectedShippingServiceId { get; set; }
        public string? SelectedShippingName { get; set; }
        public string? SelectedShippingCompany { get; set; }
        public double? SelectedShippingPrice { get; set; }
        public int? SelectedShippingDeliveryTime { get; set; }
        public string? SelectedShippingDescription { get; set; }
        public StoredCartItem() { }
        public StoredCartItem(string productId, string productTitle, string? productImage, double price, int quantity, bool isDigital) => (ProductId, ProductTitle, ProductImage, Price, Quantity, IsDigital) = (productId, productTitle, productImage, price, quantity, isDigital);
    }
    public sealed record CartItemRequest(string ProductId, int Quantity = 1, int? SelectedShippingServiceId = null);
    public sealed record CouponRequest(string Code, string? DiscountType = "Percentage", double Value = 0, double MinPurchaseAmount = 0, DateTimeOffset? StartDate = null, DateTimeOffset? EndDate = null, int? MaxUsesGlobal = null, int? MaxUsesPerUser = null, bool? IsActive = true);
    public sealed record OrderRequest(string? PaymentMethod, string? ShippingAddress, string? CustomerName, string? CustomerEmail, string? CustomerPhone, string? ZipCode);
    public sealed record StatusRequest(string Status);
    public sealed record AdminPurchaseRequest(string UserId, List<AdminPurchaseItem> Items);
    public sealed record AdminPurchaseItem(string ProductId, int Quantity = 1);
}
