using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

/// <summary>
/// Service for managing localization/internationalization of UI strings.
/// Loads JSON resource files based on the selected language and provides
/// translated strings via the Get() method.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly IJSRuntime _jsRuntime;
    private readonly IClientLocaleService _localeService;
    private Dictionary<string, JsonElement> _strings = new();
    private string _currentLanguage = "pt-BR";
    private bool _isInitialized = false;
    
    public event Action? OnLanguageChanged;

    public string CurrentLanguage => _currentLanguage;

    public static readonly Dictionary<string, LanguageInfo> SupportedLanguages = new()
    {
        { "pt-BR", new LanguageInfo("pt-BR", "Português (Brasil)", "🇧🇷") }
    };

    /// <summary>
    /// CultureInfo mappings for each supported language.
    /// Used for formatting currencies, dates, and numbers.
    /// </summary>
    private static readonly Dictionary<string, CultureInfo> LanguageCultures = new()
    {
        { "pt-BR", CultureInfo.GetCultureInfo("pt-BR") }
    };

    /// <summary>
    /// Gets the CultureInfo for the current language.
    /// </summary>
    public CultureInfo CurrentCulture => 
        LanguageCultures.TryGetValue(_currentLanguage, out var culture) 
            ? culture 
            : LanguageCultures["pt-BR"];

    public LocalizationService(HttpClient httpClient, NavigationManager navigationManager, IJSRuntime jsRuntime, IClientLocaleService localeService)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _jsRuntime = jsRuntime;
        _localeService = localeService;
    }

    /// <summary>
    /// Initializes the localization service by loading the saved language preference,
    /// or synchronizing with the IClientLocaleService (server-detected location).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            var savedLanguage = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "preferredLanguage_v2");
            if (!string.IsNullOrEmpty(savedLanguage) && SupportedLanguages.ContainsKey(savedLanguage))
            {
                _currentLanguage = savedLanguage;
            }
            else
            {
                // Sync with IClientLocaleService (which gets location from server)
                await _localeService.EnsureInitializedAsync();
                var detectedLanguage = _localeService.CurrentLocale.Language;
                
                if (!string.IsNullOrEmpty(detectedLanguage) && SupportedLanguages.ContainsKey(detectedLanguage))
                {
                    _currentLanguage = detectedLanguage;
                }
            }

            await LoadLanguageAsync(_currentLanguage);
            _isInitialized = true;
            OnLanguageChanged?.Invoke(); // Notify components to re-render with loaded translations
        }
        catch
        {
            // If any error occurs (e.g. localStorage blocked), use default language (pt-BR)
            await LoadLanguageAsync(_currentLanguage);
            _isInitialized = true;
            OnLanguageChanged?.Invoke(); 
        }
    }

    /// <summary>
    /// Loads the resource file for the specified language.
    /// Uses NavigationManager.BaseUri to ensure files are loaded from the webapp origin.
    /// </summary>
    public async Task LoadLanguageAsync(string languageCode)
    {
        if (!SupportedLanguages.ContainsKey(languageCode))
        {
            languageCode = "pt-BR";
        }

        try
        {
            // Use NavigationManager.BaseUri to get the webapp's base URL, not the API
            var baseUri = _navigationManager.BaseUri;
            // Add cache-busting parameter to prevent browser caching old translation files
            var cacheVersion = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var resourceUrl = $"{baseUri}Resources/Strings.{languageCode}.json?v={cacheVersion}";
            
            var response = await _httpClient.GetAsync(resourceUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[LocalizationService] Loaded language {languageCode}");
                _strings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
                _currentLanguage = languageCode;
                
                // Update the thread culture
                if (LanguageCultures.TryGetValue(languageCode, out var culture))
                {
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalizationService] Error loading language {languageCode}: {ex.Message}");
            // Keep existing strings if load fails
        }
    }

    /// <summary>
    /// Changes the current language and persists the preference.
    /// Also synchronizes with IClientLocaleService for category/product translations.
    /// </summary>
    public async Task SetLanguageAsync(string languageCode)
    {
        if (!SupportedLanguages.ContainsKey(languageCode))
            return;

        await LoadLanguageAsync(languageCode);
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "preferredLanguage_v2", languageCode);
            
            // SYNC: Update ClientLocaleService.CurrentLocale.Language to match
            // This ensures category/product translations use the same language
            var currentLocale = _localeService.CurrentLocale;
            currentLocale.Language = languageCode;
            await _localeService.SetLocaleAsync(currentLocale);
        }
        catch
        {
            // Ignore if localStorage is not available
        }

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Gets a translated string by key. Keys are dot-separated paths to nested values.
    /// Example: Get("Nav.Home") returns "Início" in Portuguese.
    /// </summary>
    /// <param name="key">Dot-separated path to the string (e.g., "Nav.Home", "Cart.AddToCart")</param>
    /// <returns>The translated string, or the key itself if not found</returns>
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        var parts = key.Split('.');
        
        try
        {
            if (parts.Length == 1)
            {
                // Simple key
                if (_strings.TryGetValue(key, out var value))
                {
                    return value.GetString() ?? key;
                }
            }
            else if (parts.Length >= 2)
            {
                // Nested key (e.g., "Nav.Home" or "Checkout.PaymentMethods.CreditCard")
                if (_strings.TryGetValue(parts[0], out var category))
                {
                    var current = category;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (current.ValueKind == JsonValueKind.Object && 
                            current.TryGetProperty(parts[i], out var nested))
                        {
                            current = nested;
                        }
                        else
                        {
                            return key; // Not found
                        }
                    }
                    
                    if (current.ValueKind == JsonValueKind.String)
                    {
                        return current.GetString() ?? key;
                    }
                }
            }
        }
        catch
        {
            // Return key if any error occurs
        }

        return key;
    }

    /// <summary>
    /// Gets a translated string with format arguments.
    /// Example: Get("Order.ConfirmationEmail", email) 
    /// </summary>
    public string Get(string key, params object[]? args)
    {
        var template = Get(key);
        if (args == null || args.Length == 0) return template;
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    #region Currency & Number Formatting

    /// <summary>
    /// Formats a currency value using the store's base currency (BRL).
    /// Use this for Admin pages where prices are always in BRL.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <returns>Formatted currency string (e.g., "R$ 100,00")</returns>
    public string FormatCurrency(decimal value)
    {
        // Always format as BRL (store's base currency)
        // Use CurrentCulture for number formatting (dots/commas), but keep BRL symbol if that's what we want.
        // Or if we want to show it as BRL but formatted with local rules:
        return value.ToString("C", CurrentCulture);
    }

    /// <summary>
    /// Formats a currency value using the store's base currency (BRL).
    /// Overload for double values.
    /// </summary>
    public string FormatCurrency(double value)
    {
        return FormatCurrency((decimal)value);
    }

    /// <summary>
    /// Formats a nullable decimal currency value.
    /// </summary>
    public string FormatCurrency(decimal? value)
    {
        return value.HasValue ? FormatCurrency(value.Value) : FormatCurrency(0m);
    }

    /// <summary>
    /// Formats a number using the current language's format.
    /// </summary>
    public string FormatNumber(decimal value, int decimals = 2)
    {
        return value.ToString($"N{decimals}", CurrentCulture);
    }

    #endregion

    #region Date & Time Formatting

    /// <summary>
    /// Formats a date using the current language's short date format.
    /// Example: "26/12/2025" (pt-BR), "12/26/2025" (en-US), "26.12.2025" (de-DE)
    /// </summary>
    public string FormatDate(DateTime date)
    {
        return date.ToString("d", CurrentCulture);
    }

    /// <summary>
    /// Formats a nullable date using the current language's short date format.
    /// </summary>
    public string FormatDate(DateTime? date)
    {
        return date.HasValue ? FormatDate(date.Value) : "-";
    }

    /// <summary>
    /// Formats a date using the current language's long date format.
    /// Example: "26 de dezembro de 2025" (pt-BR), "December 26, 2025" (en-US)
    /// </summary>
    public string FormatDateLong(DateTime date)
    {
        return date.ToString("D", CurrentCulture);
    }

    /// <summary>
    /// Formats a time using the current language's short time format.
    /// Example: "08:49" (pt-BR), "8:49 AM" (en-US)
    /// </summary>
    public string FormatTime(DateTime time)
    {
        return time.ToString("t", CurrentCulture);
    }

    /// <summary>
    /// Formats a nullable time using the current language's short time format.
    /// </summary>
    public string FormatTime(DateTime? time)
    {
        return time.HasValue ? FormatTime(time.Value) : "-";
    }

    /// <summary>
    /// Formats a date and time using the current language's format.
    /// Example: "26/12/2025 08:49"
    /// </summary>
    public string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToString("g", CurrentCulture);
    }

    /// <summary>
    /// Formats a nullable date and time using the current language's format.
    /// </summary>
    public string FormatDateTime(DateTime? dateTime)
    {
        return dateTime.HasValue ? FormatDateTime(dateTime.Value) : "-";
    }

    /// <summary>
    /// Formats a date and time using the current language's long format (with seconds).
    /// </summary>
    public string FormatDateTimeLong(DateTime dateTime)
    {
        return dateTime.ToString("G", CurrentCulture);
    }

    /// <summary>
    /// Formats a nullable date and time using the current language's long format.
    /// </summary>
    public string FormatDateTimeLong(DateTime? dateTime)
    {
        return dateTime.HasValue ? FormatDateTimeLong(dateTime.Value) : "-";
    }

    #endregion
}

/// <summary>
/// Information about a supported language.
/// </summary>
public record LanguageInfo(string Code, string Name, string Flag);




