using System.Net.Http.Json;
using BlazorClient.Core.Interfaces;
using BlazorClient.Models;

namespace BlazorClient.Core.Services;

public sealed class OrderService(IHttpClientFactory factory) : IOrderService
{
    private HttpClient AuthClient => factory.CreateClient("AuthenticatedAPI");
    private HttpClient PublicClient => factory.CreateClient("PublicAPI");

    public async Task<List<ShippingQuoteResponse>> CalculateShipping(CalculateShippingRequest request)
    {
        var response = await PublicClient.PostAsJsonAsync("api/shipping/quote", request);
        if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<List<ShippingQuoteResponse>>() ?? [];
        throw new InvalidOperationException(await ReadError(response, "Falha ao calcular o frete."));
    }

    public async Task<string> PlaceOrder(OrderDto dto)
    {
        var response = await AuthClient.PostAsJsonAsync("api/orders", dto);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadError(response, "Falha ao criar o pedido."));
        return (await response.Content.ReadFromJsonAsync<Order>())?.Id ?? throw new InvalidOperationException("Pedido criado sem identificação.");
    }

    public async Task<OrderListResponse> GetMyOrders(int page = 1, int pageSize = 10, string? lastDocId = null)
        => await AuthClient.GetFromJsonAsync<OrderListResponse>($"api/orders?page={page}&pageSize={pageSize}") ?? new OrderListResponse([], new PaginationData());

    public async Task<Order?> GetOrderById(string id)
    {
        var response = await AuthClient.GetAsync($"api/orders/{Uri.EscapeDataString(id)}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Order>() : null;
    }

    public async Task<List<Order>> GetAllOrders() => (await GetAdminOrders()).Items;

    public async Task<OrderListResponse> GetAdminOrders(int page = 1, int pageSize = 50, string? status = null, string? lastDocId = null)
    {
        var url = $"api/orders/admin/all?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status)) url += $"&status={Uri.EscapeDataString(status)}";
        return await AuthClient.GetFromJsonAsync<OrderListResponse>(url) ?? new OrderListResponse([], new PaginationData(page, pageSize, 0, 1, null));
    }

    public async Task UpdateOrderStatus(string id, string status)
    {
        var response = await AuthClient.PutAsJsonAsync($"api/orders/{Uri.EscapeDataString(id)}/status", new { Status = status });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadError(response, "Falha ao atualizar o status."));
    }

    public async Task DeleteOrder(string id)
    {
        var response = await AuthClient.DeleteAsync($"api/orders/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadError(response, "Falha ao excluir o pedido."));
    }

    public async Task CreateAdminPurchase(AdminPurchaseRequest request)
    {
        var response = await AuthClient.PostAsJsonAsync("api/orders/admin-purchase", request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadError(response, "Falha ao registrar a compra manual."));
    }

    public async Task<bool> HasPurchasedProduct(string productId)
    {
        var response = await AuthClient.GetAsync($"api/orders/check-ownership/{Uri.EscapeDataString(productId)}");
        return response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<bool>();
    }

    public async Task<DashboardStats?> GetDashboardStatsAsync()
    {
        var response = await AuthClient.GetAsync("api/admin/stats");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<DashboardStats>() : null;
    }

    private static async Task<string> ReadError(HttpResponseMessage response, string fallback)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(body);
            foreach (var field in new[] { "message", "error", "title" })
                if (json.RootElement.TryGetProperty(field, out var value) && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!;
        }
        catch (System.Text.Json.JsonException) { }
        return fallback;
    }
}
