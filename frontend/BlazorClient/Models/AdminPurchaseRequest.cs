namespace BlazorClient.Models;

public class AdminPurchaseRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<AdminPurchaseItem> Items { get; set; } = new();
}

public class AdminPurchaseItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
