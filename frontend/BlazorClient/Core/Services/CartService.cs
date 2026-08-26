using System.Net.Http.Json;
using BlazorClient.Models;
using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

public class CartService : ICartService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;
    private Cart? _cart;

    public event Action? OnChange;

    public CartService(IHttpClientFactory factory, IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");

    public async Task<Cart?> GetCart(string? currency = null)
    {
        var user = await _authService.GetCurrentUser();
        if (user == null || string.IsNullOrEmpty(user.Uid))
        {
            _cart = null; // No cart for guests yet, or local storage implementation could be added later
            return null;
        }

        try
        {
            var url = "api/carts";
            if (!string.IsNullOrEmpty(currency)) url += $"?currency={currency}";
            
            _cart = await AuthClient.GetFromJsonAsync<Cart>(url);
            OnChange?.Invoke(); // Update UI
            return _cart;
        }
        catch
        {
            // Handle error (maybe user has no cart yet, or API error)
            return null;
        }
    }

    private Cart CloneCart(Cart cart)
    {
        return new Cart
        {
            Id = cart.Id,
            UpdatedAt = cart.UpdatedAt,
            CouponCode = cart.CouponCode,
            DiscountAmount = cart.DiscountAmount,
            Items = cart.Items.Select(i => new CartItem 
            {
                ProductId = i.ProductId,
                ProductTitle = i.ProductTitle,
                ProductImage = i.ProductImage,
                Price = i.Price,
                Quantity = i.Quantity,
                Color = i.Color,
                Size = i.Size,
                IsSubscription = i.IsSubscription,
                IsDigital = i.IsDigital,
                Width = i.Width,
                Height = i.Height,
                Length = i.Length,
                Weight = i.Weight,
                SelectedShippingServiceId = i.SelectedShippingServiceId,
                SelectedShippingName = i.SelectedShippingName,
                SelectedShippingCompany = i.SelectedShippingCompany,
                SelectedShippingPrice = i.SelectedShippingPrice,
                SelectedShippingDeliveryTime = i.SelectedShippingDeliveryTime,
                SelectedShippingDescription = i.SelectedShippingDescription
            }).ToList()
        };
    }

    public async Task AddToCart(CartItem item, string? currency = null)
    {
        // 1. Snapshot for rollback
        var previousCart = _cart != null ? CloneCart(_cart) : null;

        // 2. Validação local (feedback imediato)
        if (_cart != null)
        {
            // Regra: Assinatura deve ser única no carrinho
            if (item.IsSubscription && _cart.Items.Any())
            {
                throw new InvalidOperationException("Assinaturas devem ser compradas individualmente. Por favor, limpe seu carrinho primeiro.");
            }
            if (_cart.Items.Any(i => i.IsSubscription))
            {
                throw new InvalidOperationException("Você já possui uma assinatura no carrinho. Assinaturas devem ser compradas individualmente.");
            }
        }

        // 3. Optimistic Update
        _cart ??= new Cart { Items = new List<CartItem>() };
        
        var existingItem = _cart.Items.FirstOrDefault(i => i.ProductId == item.ProductId && i.Color == item.Color && i.Size == item.Size);
        if (existingItem != null)
        {
            // Regra: Digital/Assinatura não pode ter quantidade > 1
            if (existingItem.IsSubscription || existingItem.IsDigital)
            {
                throw new InvalidOperationException("Este produto já está no seu carrinho. Produtos digitais e assinaturas são limitados a 1 unidade.");
            }
            existingItem.Quantity += item.Quantity;
        }
        else
        {
            // Create a copy of the item to avoid reference issues
            var newItem = new CartItem
            {
                ProductId = item.ProductId,
                ProductTitle = item.ProductTitle,
                ProductImage = item.ProductImage,
                Price = item.Price,
                Quantity = (item.IsSubscription || item.IsDigital) ? 1 : item.Quantity,
                Color = item.Color,
                Size = item.Size,
                IsSubscription = item.IsSubscription,
                IsDigital = item.IsDigital
            };
            _cart.Items.Add(newItem); 
        }
        
        OnChange?.Invoke(); // UI updates instantly

        try
        {
            var url = "api/carts/items";
            if (!string.IsNullOrEmpty(currency)) url += $"?currency={currency}";
            
            var response = await AuthClient.PostAsJsonAsync(url, item);
            if (response.IsSuccessStatusCode)
            {
                _cart = await response.Content.ReadFromJsonAsync<Cart>();
                OnChange?.Invoke();
            }
            else
            {
                // Revert
                _cart = previousCart;
                OnChange?.Invoke();
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized");
                }
                
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro ao adicionar ao carrinho: {error}");
            }
        }
        catch (Exception)
        {
            _cart = previousCart;
            OnChange?.Invoke();
            throw; // Re-throw to let caller handle it
        }
    }

    public async Task UpdateQuantity(string productId, int quantity, string? color = null, string? size = null, string? currency = null)
    {
        // 1. Snapshot
        var previousCart = _cart != null ? CloneCart(_cart) : null;

        // 2. Optimistic Update
        if (_cart != null)
        {
            var existingItem = _cart.Items.FirstOrDefault(i => i.ProductId == productId && i.Color == color && i.Size == size);
            if (existingItem != null)
            {
                // Regra: Digital/Assinatura não pode ter quantidade > 1
                if ((existingItem.IsSubscription || existingItem.IsDigital) && quantity > 1)
                {
                    throw new InvalidOperationException("Produtos digitais e assinaturas são limitados a quantidade 1.");
                }
                existingItem.Quantity = quantity;
                OnChange?.Invoke();
            }
        }

        try
        {
            var query = $"?color={color}&size={size}";
            if (!string.IsNullOrEmpty(currency)) query += $"&currency={currency}";
            
            var response = await AuthClient.PutAsJsonAsync($"api/carts/items/{productId}{query}", quantity);
            if (response.IsSuccessStatusCode)
            {
                _cart = await response.Content.ReadFromJsonAsync<Cart>();
                OnChange?.Invoke();
            }
            else
            {
                _cart = previousCart;
                OnChange?.Invoke();
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized");
                }
                
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro ao atualizar quantidade: {error}");
            }
        }
        catch
        {
            _cart = previousCart;
            OnChange?.Invoke();
            throw;
        }
    }

    public async Task UpdateShippingZipCode(string zipCode)
    {
        try
        {
            var response = await AuthClient.PutAsJsonAsync("api/carts/shipping-zip-code", zipCode);
            if (response.IsSuccessStatusCode)
            {
                var updated = await response.Content.ReadFromJsonAsync<Cart>();
                if (updated != null)
                {
                    _cart = updated;
                    // Dont invoke OnChange here to avoid flickering if not needed, or do it if needed
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating zip code: {ex.Message}");
        }
    }

    public async Task RemoveItem(string productId, string? color = null, string? size = null, string? currency = null)
    {
        // 1. Snapshot
        var previousCart = _cart != null ? CloneCart(_cart) : null;

        // 2. Optimistic Update
        if (_cart != null)
        {
            var itemToRemove = _cart.Items.FirstOrDefault(i => i.ProductId == productId && i.Color == color && i.Size == size);
            if (itemToRemove != null)
            {
                _cart.Items.Remove(itemToRemove);
                OnChange?.Invoke();
            }
        }

        try
        {
            var query = $"?color={color}&size={size}";
            if (!string.IsNullOrEmpty(currency)) query += $"&currency={currency}";

            var response = await AuthClient.DeleteAsync($"api/carts/items/{productId}{query}");
            if (response.IsSuccessStatusCode)
            {
                _cart = await response.Content.ReadFromJsonAsync<Cart>();
                OnChange?.Invoke();
            }
            else
            {
                _cart = previousCart;
                OnChange?.Invoke();
                
                 if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized");
                }
                
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro ao remover item: {error}");
            }
        }
        catch (Exception)
        {
            _cart = previousCart;
            OnChange?.Invoke();
            throw;
        }
    }

    public async Task ClearCart()
    {
        // 1. Snapshot
        var previousCart = _cart != null ? CloneCart(_cart) : null;

        // 2. Optimistic Update
        _cart = new Cart();
        OnChange?.Invoke();

        try
        {
            var response = await AuthClient.DeleteAsync("api/carts");
            if (response.IsSuccessStatusCode)
            {
                _cart = new Cart(); 
                OnChange?.Invoke();
            }
            else
            {
                _cart = previousCart;
                OnChange?.Invoke();
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new Exception("Unauthorized");
                }
                 
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro ao limpar carrinho: {error}");
            }
        }
        catch (Exception)
        {
            _cart = previousCart;
            OnChange?.Invoke();
            throw;
        }
    }

    public async Task<int> GetCartItemCount()
    {
        if (_cart == null)
        {
             await GetCart();
        }
        
        return _cart?.Items.Sum(i => i.Quantity) ?? 0;
    }

    public async Task ApplyCoupon(string code, string? currency = null)
    {
        var url = "api/carts/apply-coupon";
        if (!string.IsNullOrEmpty(currency)) url += $"?currency={currency}";

        var response = await AuthClient.PostAsJsonAsync(url, new ApplyCouponDto { Code = code });
        if (response.IsSuccessStatusCode)
        {
            _cart = await response.Content.ReadFromJsonAsync<Cart>();
            OnChange?.Invoke();
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }
}




