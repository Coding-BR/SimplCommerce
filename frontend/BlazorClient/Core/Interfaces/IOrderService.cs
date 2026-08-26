using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IOrderService
{
    Task<string> PlaceOrder(OrderDto dto);
    Task<OrderListResponse> GetMyOrders(int page = 1, int pageSize = 10, string? lastDocId = null);
    Task<Order?> GetOrderById(string id);
    Task<List<Order>> GetAllOrders();
    Task UpdateOrderStatus(string id, string status);
    Task DeleteOrder(string id);
    Task<OrderListResponse> GetAdminOrders(int page = 1, int pageSize = 50, string? status = null, string? lastDocId = null);
    Task CreateAdminPurchase(AdminPurchaseRequest request);
    Task<List<ShippingQuoteResponse>> CalculateShipping(CalculateShippingRequest request);
    
    /// <summary>
    /// Check if the authenticated user has purchased a specific product
    /// </summary>
    Task<bool> HasPurchasedProduct(string productId);
    
    /// <summary>
    /// Gets dashboard statistics for the admin panel
    /// </summary>
    Task<DashboardStats?> GetDashboardStatsAsync();
}
