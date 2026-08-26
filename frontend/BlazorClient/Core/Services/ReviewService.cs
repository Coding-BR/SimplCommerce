using System.Net.Http.Json;
using BlazorClient.Models;
using BlazorClient.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorClient.Core.Services;

public class ReviewService : IReviewService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(IHttpClientFactory httpClientFactory, ILogger<ReviewService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // --- PUBLIC METHODS ---

    public async Task<ReviewListResponse?> GetReviews(string productId, int pageSize = 5, string? lastDocId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PublicAPI");
            var url = $"api/reviews/{productId}?pageSize={pageSize}";
            if (!string.IsNullOrEmpty(lastDocId))
            {
                url += $"&lastDocId={lastDocId}";
            }

            return await client.GetFromJsonAsync<ReviewListResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch reviews");
            return null;
        }
    }

    public async Task<ProductReview?> AddReview(ProductReview review)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AuthenticatedAPI");
            var response = await client.PostAsJsonAsync("api/reviews", review);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ProductReview>();
            }
            
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to add review: {StatusCode} {Error}", response.StatusCode, error);
            throw new Exception($"Error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception adding review");
            throw;
        }
    }

    // --- ADMIN METHODS ---

    public async Task<ReviewListResponse?> GetAdminReviews(int pageSize = 10, string? lastDocId = null, string? productId = null)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AuthenticatedAPI");
            var url = $"api/reviews/admin?pageSize={pageSize}";
            if (!string.IsNullOrEmpty(lastDocId)) url += $"&lastDocId={lastDocId}";
            if (!string.IsNullOrEmpty(productId)) url += $"&productId={productId}";

            return await client.GetFromJsonAsync<ReviewListResponse>(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch admin reviews");
            return null;
        }
    }

    public async Task DeleteReview(string id)
    {
        var client = _httpClientFactory.CreateClient("AuthenticatedAPI");
        var response = await client.DeleteAsync($"api/reviews/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateReview(ProductReview review)
    {
        var client = _httpClientFactory.CreateClient("AuthenticatedAPI");
        var response = await client.PutAsJsonAsync($"api/reviews/{review.Id}", review);
        response.EnsureSuccessStatusCode();
    }
}




