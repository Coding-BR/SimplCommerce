using System.Net.Http.Json;
using BlazorClient.Models;
using Microsoft.AspNetCore.Components.Forms;

using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly IHttpClientFactory _factory;
    private readonly ILocalizationService _localizationService;
    
    // Cache local em memória
    private SiteSettings? _cachedSettings;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1); // Settings mudam pouco
    
    // Event to notify components when settings change
    public event Action? OnSettingsChanged;

    public SettingsService(IHttpClientFactory factory, ILocalizationService localizationService)
    {
        _factory = factory;
        _localizationService = localizationService;
        
        // Listener para mudança de idioma - limpar cache para reaplicar traduções
        _localizationService.OnLanguageChanged += ClearCache;
    }

    private HttpClient PublicClient => _factory.CreateClient("PublicAPI");
    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");

    public async Task<SiteSettings> GetSettings()
    {
        try
        {
            // Verificar se cache ainda é válido
            if (_cachedSettings != null && DateTime.UtcNow < _cacheExpiry)
            {
                Console.WriteLine("SettingsService: Returning settings from local cache");
                return ApplyLocalTranslations(_cachedSettings);
            }

            // Cache expirado ou não existe - buscar da API  
            Console.WriteLine("SettingsService: Fetching settings from API");
            var settings = await PublicClient.GetFromJsonAsync<SiteSettings>("api/settings/public");
            
            if (settings != null)
            {
                // Cachear settings
                _cachedSettings = settings;
                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                Console.WriteLine($"SettingsService: Settings cached until {_cacheExpiry}");
                
                return ApplyLocalTranslations(settings);
            }
            
            return new SiteSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching settings: {ex.Message}");
            return _cachedSettings != null 
                ? ApplyLocalTranslations(_cachedSettings) // Retornar cache mesmo expirado se API falhar
                : new SiteSettings(); // Última opção: retornar default
        }
    }

    /// <summary>
    /// Aplica traduções localmente baseado no idioma atual do LocalizationService
    /// </summary>
    private SiteSettings ApplyLocalTranslations(SiteSettings settings)
    {
        // Se não há traduções, retornar original
        if (settings.Translations == null || !settings.Translations.Any())
        {
            return settings;
        }

        var currentLang = _localizationService.CurrentLanguage;

        // Se não há tradução para o idioma atual, retornar original
        if (!settings.Translations.ContainsKey(currentLang))
        {
            Console.WriteLine($"SettingsService: No translation for {currentLang}, using original");
            return settings;
        }

        // Aplicar tradução
        var translation = settings.Translations[currentLang];
        
        var translatedSettings = new SiteSettings
        {
            Id = settings.Id,
            TopBarMessage = !string.IsNullOrEmpty(translation.TopBarMessage) 
                ? translation.TopBarMessage 
                : settings.TopBarMessage,
            Contact = settings.Contact,
            SocialLinks = settings.SocialLinks,
            Features = new List<FeatureItem>(),
            LogoUrl = settings.LogoUrl,
            Translations = settings.Translations // Manter para referência
        };

        // Aplicar traduções de Features se existirem
        if (translation.Features != null && translation.Features.Any())
        {
            for (int i = 0; i < settings.Features.Count; i++)
            {
                var originalFeature = settings.Features[i];
                
                // Se há tradução para este feature, usar
                if (i < translation.Features.Count)
                {
                    var featureTranslation = translation.Features[i];
                    translatedSettings.Features.Add(new FeatureItem
                    {
                        Title = !string.IsNullOrEmpty(featureTranslation.Title)
                            ? featureTranslation.Title
                            : originalFeature.Title,
                        Subtitle = !string.IsNullOrEmpty(featureTranslation.Subtitle)
                            ? featureTranslation.Subtitle
                            : originalFeature.Subtitle,
                        IconClass = originalFeature.IconClass,
                        IsEnabled = originalFeature.IsEnabled
                    });
                }
                else
                {
                    // Sem tradução para este índice, usar original
                    translatedSettings.Features.Add(originalFeature);
                }
            }
        }
        else
        {
            // Sem traduções de features, usar originais
            translatedSettings.Features = settings.Features;
        }

        Console.WriteLine($"SettingsService: Applied {currentLang} translations");
        return translatedSettings;
    }

    public void ClearCache()
    {
        _cachedSettings = null;
        _cacheExpiry = DateTime.MinValue;
        Console.WriteLine("SettingsService: Cache cleared");
    }

    public async Task UpdateSettings(SiteSettings settings)
    {
        var response = await AuthClient.PostAsJsonAsync("api/settings", settings);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to update settings: {error}");
        }
        
        // Limpar cache após atualização
        ClearCache();
        
        // Notify all listeners that settings have changed
        OnSettingsChanged?.Invoke();
    }

    public async Task<string> UploadLogoAsync(IBrowserFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);

            var response = await AuthClient.PostAsync("api/logo/upload", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Upload failed: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
            var logoUrl = result?.Url ?? throw new Exception("No logo URL returned");
            
            // Limpar cache após upload
            ClearCache();
            
            // Notify all listeners that settings have changed
            OnSettingsChanged?.Invoke();
            
            return logoUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading logo: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteLogoAsync()
    {
        var response = await AuthClient.DeleteAsync("api/logo");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to delete logo: {error}");
        }
        
        // Limpar cache após deletar
        ClearCache();
        
        // Notify all listeners that settings have changed
        OnSettingsChanged?.Invoke();
    }

    public async Task<string?> GetLogoUrlAsync()
    {
        try
        {
            var response = await PublicClient.GetFromJsonAsync<LogoUrlResponse>("api/logo");
            return response?.LogoUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching logo URL: {ex.Message}");
            return null;
        }
    }

    public async Task<IntegrationSettingsModel> GetIntegrationSettingsAsync()
    {
        return await AuthClient.GetFromJsonAsync<IntegrationSettingsModel>("api/settings/integrations")
            ?? new IntegrationSettingsModel();
    }

    public async Task<IntegrationSettingsModel> UpdateIntegrationSettingsAsync(IntegrationSettingsModel settings)
    {
        var response = await AuthClient.PutAsJsonAsync("api/settings/integrations", settings);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Não foi possível salvar as integrações: {error}");
        }

        return await response.Content.ReadFromJsonAsync<IntegrationSettingsModel>()
            ?? throw new InvalidOperationException("A API não retornou as configurações salvas.");
    }
}

// Helper response classes
public record LogoUrlResponse(string? LogoUrl);





