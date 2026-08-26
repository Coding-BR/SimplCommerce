namespace BlazorClient.Core.Navigation;

public static class AppRoutes
{
    // Root
    public const string Home = "/";
    public const string Shop = "/shop";
    public const string Cart = "/cart";
    public const string Checkout = "/checkout";
    public const string CheckoutReturn = "/checkout/return";
    public const string Cupons = "/cupons";

    // Auth
    public const string Login = "/login";
    public const string Register = "/register";

    // Product
    public const string ProductDetails = "/product/{id}";

    // My Account
    public const string MyAccount = "/my-account";
    public const string MyAccountWithTab = "/my-account/{ActiveTab?}";
    public const string MyOrderDetails = "/my-account/order/{Id}";

    // Admin
    public const string AdminDashboard = "/admin";
    public const string AdminOrders = "/admin/orders";
    public const string AdminCreateOrder = "/admin/orders/create/{UserId}";
    public const string AdminProducts = "/admin/products";
    public const string AdminProductsNew = "/admin/products/new";
    public const string AdminProductsEdit = "/admin/products/edit/{Id}";
    public const string AdminUsers = "/admin/users";
    public const string AdminSettings = "/admin/settings";
    public const string AdminCoupons = "/admin/coupons";
    public const string AdminFiles = "/admin/files";
    public const string AdminReviews = "/admin/reviews";
}
