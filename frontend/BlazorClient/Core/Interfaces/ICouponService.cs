using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface ICouponService
{
    Task<List<Coupon>> GetCoupons();
    Task<Coupon?> GetCoupon(string code);
    Task<List<Coupon>> GetPublicCoupons();
    Task CreateCoupon(CreateCouponDto coupon);
    Task UpdateCoupon(string code, UpdateCouponDto coupon);
    Task DeleteCoupon(string code);
}
