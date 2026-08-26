using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

using BlazorClient.Models;

/// <summary>
/// Interface for payment operations
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Initiates a payment for an order
    /// </summary>
    /// <param name="orderId">The order to pay for</param>
    /// <param name="provider">Payment provider (PayPal, Stripe, etc.)</param>
    /// <param name="returnUrl">URL to return after approval</param>
    /// <param name="cancelUrl">URL if user cancels</param>
    /// <returns>Payment response with approval URL</returns>
    Task<PaymentInitResponse> InitiatePaymentAsync(string orderId, string provider, string returnUrl, string cancelUrl);

    /// <summary>
    /// Captures a payment after user approval
    /// </summary>
    Task<PaymentCaptureResponse> CapturePaymentAsync(string orderId, string? payerId = null, string? token = null);

    /// <summary>
    /// Gets available payment providers
    /// </summary>
    Task<List<string>> GetAvailableProvidersAsync();

}
