using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface ICartService
{
    event Action? OnChange;
    Task<Cart?> GetCart(string? currency = null);
    Task AddToCart(CartItem item, string? currency = null);
    Task UpdateQuantity(string productId, int quantity, string? color = null, string? size = null, string? currency = null);
    Task RemoveItem(string productId, string? color = null, string? size = null, string? currency = null);
    Task ClearCart();
    Task<int> GetCartItemCount();
    Task ApplyCoupon(string code, string? currency = null);
    Task UpdateShippingZipCode(string zipCode);
}
