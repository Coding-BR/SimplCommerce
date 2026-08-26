using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IReviewService
{
    // Public
    Task<ReviewListResponse?> GetReviews(string productId, int pageSize = 5, string? lastDocId = null);
    Task<ProductReview?> AddReview(ProductReview review);

    // Admin
    Task<ReviewListResponse?> GetAdminReviews(int pageSize = 10, string? lastDocId = null, string? productId = null);
    Task DeleteReview(string id);
    Task UpdateReview(ProductReview review);
}
