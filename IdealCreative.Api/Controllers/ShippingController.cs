using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdealCreative.Api.Services;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public sealed class ShippingController(IntegrationSettingsStore integrations) : ControllerBase
{
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> Quote(ShippingRequest request, CancellationToken ct)
    {
        var settings = (await integrations.GetRuntimeAsync(ct)).Shipping;
        var physical = request.Products.Where(item => item.Quantity > 0).ToList(); if (physical.Count == 0) return Ok(Array.Empty<object>());
        var quotes = new List<ShippingQuote>();
        if (settings.LocalPickupEnabled)
            quotes.Add(new ShippingQuote(3, "Retirada local", "IdealCreative", 0m, 0m, 0m, "R$", settings.PickupPreparationDays, 0, PickupDescription(settings)));

        if (!settings.Enabled) return quotes.Count > 0 ? Ok(quotes) : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Nenhuma forma de entrega está habilitada." });
        if (string.IsNullOrWhiteSpace(request.ToZipCode)) return quotes.Count > 0 ? Ok(quotes) : BadRequest(new { error = "CEP obrigatório." });
        if (settings.Provider.Equals("MelhorEnvio", StringComparison.OrdinalIgnoreCase))
            return quotes.Count > 0 ? Ok(quotes) : StatusCode(StatusCodes.Status501NotImplemented, new { error = "O adaptador de cotação do Melhor Envio ainda não está habilitado." });
        var weight = Math.Max(1m, physical.Sum(item => item.Weight * item.Quantity)); var price = settings.BasePrice + Math.Min(settings.MaxAdditionalPrice, weight * settings.PricePerKg);
        quotes.Add(new ShippingQuote(1, "Entrega econômica", "IdealCreative", price, price, 0m, "R$", settings.EconomyDeliveryDays, 2, null));
        quotes.Add(new ShippingQuote(2, "Entrega expressa", "IdealCreative", price + settings.ExpressAdditionalPrice, price + settings.ExpressAdditionalPrice, 0m, "R$", settings.ExpressDeliveryDays, 1, null));
        return Ok(quotes);
    }

    [HttpGet("balance")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Balance(CancellationToken ct)
    {
        var settings = (await integrations.GetRuntimeAsync(ct)).Shipping;
        return Ok(new { available = settings.Enabled, provider = settings.Provider, tokenConfigured = !string.IsNullOrWhiteSpace(settings.ApiToken), message = settings.Provider == "Local" ? "Cotações locais habilitadas." : "Credencial armazenada; adaptador externo ainda não habilitado." });
    }

    public sealed record ShippingRequest(string ToZipCode, List<ShippingProduct> Products);
    public sealed record ShippingProduct(string Id, int Width, int Height, int Length, decimal Weight, decimal InsuranceValue, int Quantity = 1);
    private sealed record ShippingQuote(int Id, string Name, string Company, decimal Price, decimal CustomPrice, decimal Discount, string Currency, int DeliveryTime, int? DeliveryRange, string? Description);
    private static string PickupDescription(ShippingRuntimeSettings settings) => string.Join(" · ", new[] { settings.PickupAddress, settings.PickupInstructions }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
