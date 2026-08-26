using System.Net.Http.Json;
using BlazorClient.Models;
using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

public class CouponService : ICouponService
{
    private readonly IHttpClientFactory _factory;

    public CouponService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");
    private HttpClient PublicClient => _factory.CreateClient("PublicAPI");

    public async Task<List<Coupon>> GetCoupons()
    {
        return await AuthClient.GetFromJsonAsync<List<Coupon>>("api/coupons") ?? new List<Coupon>();
    }

    public async Task<List<Coupon>> GetPublicCoupons()
    {
        try
        {
            return await PublicClient.GetFromJsonAsync<List<Coupon>>("api/coupons/public") ?? new List<Coupon>();
        }
        catch
        {
            return new List<Coupon>();
        }
    }

    public async Task<Coupon?> GetCoupon(string code)
    {
        try
        {
            return await AuthClient.GetFromJsonAsync<Coupon>($"api/coupons/{code}");
        }
        catch
        {
            return null;
        }
    }

    public async Task CreateCoupon(CreateCouponDto coupon)
    {
        var response = await AuthClient.PostAsJsonAsync("api/coupons", coupon);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create coupon: {error}");
        }
    }

    public async Task UpdateCoupon(string code, UpdateCouponDto coupon)
    {
        var response = await AuthClient.PutAsJsonAsync($"api/coupons/{code}", coupon);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to update coupon: {error}");
        }
    }

    public async Task DeleteCoupon(string code)
    {
        var response = await AuthClient.DeleteAsync($"api/coupons/{code}");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to delete coupon: {error}");
        }
    }
}




