using BlazorClient;
using BlazorClient.Core.Interfaces;
using BlazorClient.Core.Services;
using BlazorClient.Core.Handlers;
using BlazorClient.Core.Navigation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;

using System.Globalization;

using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Set culture based on user preference or detection (handled by LocalizationService)
// var culture = new CultureInfo("pt-BR");
// CultureInfo.DefaultThreadCurrentCulture = culture;
// CultureInfo.DefaultThreadCurrentUICulture = culture;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var frontendBaseUri = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseUrl = frontendBaseUri.Host is "localhost" or "127.0.0.1"
    ? $"{frontendBaseUri.Scheme}://{frontendBaseUri.Host}:5288"
    : frontendBaseUri.ToString();

// Authentication Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => 
    provider.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

// HTTP Handlers
builder.Services.AddScoped<AuthenticationMessageHandler>();

// Public API client
builder.Services.AddHttpClient("PublicAPI", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Authenticated API client (IdealCreative JWT)
builder.Services.AddHttpClient("AuthenticatedAPI", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
})
.AddHttpMessageHandler<AuthenticationMessageHandler>();

// Register default HttpClient for convenience (optional, but safer to rely on factory)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("PublicAPI"));

// Other Services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

builder.Services.AddScoped<IClientLocaleService, ClientLocaleService>();

// Localization Service for i18n (Singleton to maintain state across all components)
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

builder.Services.AddMudServices();

var host = builder.Build();

// Initialize core services before running the app to avoid deadlocks
var localeService = host.Services.GetRequiredService<IClientLocaleService>();
await localeService.InitializeAsync();

var localizationService = host.Services.GetRequiredService<ILocalizationService>();
await localizationService.InitializeAsync();

await host.RunAsync();
