using BlazorClient.Core.Interfaces;
using BlazorClient.Models;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace BlazorClient.Core.Services;

public class ClientLocaleService : IClientLocaleService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private UserLocale _currentLocale = new UserLocale
    {
        Language = "pt-BR",
        Currency = "BRL",
        CountryCode = "BR",
        CurrencySymbol = "R$"
    };

    public event Action? OnChange;

    public UserLocale CurrentLocale => _currentLocale;

    public ClientLocaleService(IJSRuntime jsRuntime, HttpClient httpClient, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
        _navigationManager = navigationManager;
    }

    private TaskCompletionSource _initTcs = new TaskCompletionSource();
    public bool IsInitialized => _initTcs.Task.IsCompleted;

    public async Task EnsureInitializedAsync()
    {
        await _initTcs.Task;
    }

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        try
        {
            // A loja opera em uma única região/moeda. Não fazemos GeoIP nem
            // chamada de detecção externa: isso deixa o PWA previsível e leve.
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "user_locale_v2");
            await SetLocaleAsync(_currentLocale);
        }
        catch
        {
            // Fallback to default (BRL)
        }
        finally
        {
            if (!_initTcs.Task.IsCompleted)
            {
                _initTcs.SetResult();
            }
        }
    }

    public async Task SetLocaleAsync(UserLocale locale)
    {
        _currentLocale = locale;
        var json = System.Text.Json.JsonSerializer.Serialize(locale);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "user_locale_v2", json);
        NotifyStateChanged();
    }

    public string FormatPrice(double originalPrice, decimal? displayPrice, string? displayCurrency)
    {
        // Use Display values if present, else original
        decimal finalPrice = displayPrice ?? (decimal)originalPrice;
        string currency = displayCurrency ?? "BRL";

        // Simple formatting
        // In real app, use CultureInfo based on _currentLocale.CountryCode/Language
        try 
        {
            var cultureName = _currentLocale.Language; // e.g., "pt-BR", "en-US"
            var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
            
            // Format format: "C" uses currency symbol of the CULTURE, not the currency code!
            // If culture is pt-BR, it displays R$. If currency is USD, we want U$ (or $).
            // Default .NET "C" might mismatch.
            // Better: Manual format or specific formatter.
            // Using logic: Symbol + Value.ToString("N2")
            
            return $"{_currentLocale.CurrencySymbol} {finalPrice.ToString("N2", culture)}";
        }
        catch
        {
            // Fallback
            return $"{_currentLocale.CurrencySymbol} {finalPrice:N2}";
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public class DetectLocaleResponse
{
    public UserLocale Locale { get; set; } = new();
    public decimal ExchangeRate { get; set; }
}




