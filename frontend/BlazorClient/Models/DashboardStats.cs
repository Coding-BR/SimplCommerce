using System.Text.Json.Serialization;

namespace BlazorClient.Models;

public class DashboardStats
{
    [JsonPropertyName("totalProducts")]
    public int TotalProducts { get; set; }
    
    [JsonPropertyName("totalOrders")]
    public int TotalOrders { get; set; }
    
    [JsonPropertyName("totalUsers")]
    public int TotalUsers { get; set; }
    
    [JsonPropertyName("totalSales")]
    public decimal TotalSales { get; set; }
}
