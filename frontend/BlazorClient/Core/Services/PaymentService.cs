using System.Net.Http.Json;
using BlazorClient.Models;
using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

/// <summary>
/// Service for payment operations via API
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IHttpClientFactory _factory;

    public PaymentService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");
    private HttpClient PublicClient => _factory.CreateClient("PublicAPI");

    public async Task<PaymentInitResponse> InitiatePaymentAsync(string orderId, string provider, string returnUrl, string cancelUrl)
    {
        try
        {
            var request = new
            {
                OrderId = orderId,
                Provider = provider,
                ReturnUrl = returnUrl,
                CancelUrl = cancelUrl
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await AuthClient.PostAsJsonAsync("api/payments/create", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Payment initiation failed: Status={response.StatusCode}, Content={content}");
                
                // Try to extract detailed error message from API response
                try 
                {
                    var errorObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content, options);
                    if (errorObj != null)
                    {
                        if (errorObj.TryGetValue("error", out var error))
                        {
                            return new PaymentInitResponse { Success = false, Error = error.ToString() };
                        }
                        if (errorObj.TryGetValue("details", out var details))
                        {
                            return new PaymentInitResponse { Success = false, Error = details.ToString() };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to parse error response: {ex.Message}");
                }
                
                return new PaymentInitResponse { Success = false, Error = $"HTTP {response.StatusCode}: {content}" };
            }

            var result = System.Text.Json.JsonSerializer.Deserialize<PaymentInitApiResponse>(content, options);
            
            Console.WriteLine($"Payment API Response: {content}"); // Debug log

            return new PaymentInitResponse
            {
                Success = true,
                PaymentId = result?.PaymentId ?? "",
                ApprovalUrl = result?.ApprovalUrl,
                Provider = result?.Provider ?? provider
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initiating payment: {ex.Message}");
            return new PaymentInitResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<PaymentCaptureResponse> CapturePaymentAsync(string orderId, string? payerId = null, string? token = null)
    {
        try
        {
            var request = new
            {
                PayerId = payerId,
                Token = token
            };

            var response = await AuthClient.PostAsJsonAsync($"api/payments/capture/{orderId}", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Payment capture failed: {error}");
                return new PaymentCaptureResponse { Success = false, Error = error };
            }

            var result = await response.Content.ReadFromJsonAsync<PaymentCaptureApiResponse>();
            
            return new PaymentCaptureResponse
            {
                Success = result?.Success ?? false,
                TransactionId = result?.TransactionId,
                Status = result?.Status ?? "",
                OrderId = orderId
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error capturing payment: {ex.Message}");
            return new PaymentCaptureResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<string>> GetAvailableProvidersAsync()
    {
        try
        {
            var providers = await PublicClient.GetFromJsonAsync<List<string>>("api/payments/providers");
            return providers ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching providers: {ex.Message}");
            return new List<string> { "PayPal" }; // Default fallback
        }
    }
}

// API response models
internal class PaymentInitApiResponse
{
    public string PaymentId { get; set; } = string.Empty;
    public string? ApprovalUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}

internal class PaymentCaptureApiResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
}




