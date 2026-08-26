using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IClientLocaleService
{
    event Action? OnChange;
    UserLocale CurrentLocale { get; }
    bool IsInitialized { get; }
    Task EnsureInitializedAsync();
    Task InitializeAsync();
    Task SetLocaleAsync(UserLocale locale);
    string FormatPrice(double originalPrice, decimal? displayPrice, string? displayCurrency);
}
