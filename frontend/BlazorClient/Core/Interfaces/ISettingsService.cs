using BlazorClient.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorClient.Core.Interfaces;

public interface ISettingsService
{
    Task<SiteSettings> GetSettings();
    Task UpdateSettings(SiteSettings settings);
    Task<string> UploadLogoAsync(IBrowserFile file);
    Task DeleteLogoAsync();
    Task<string?> GetLogoUrlAsync();
    Task<IntegrationSettingsModel> GetIntegrationSettingsAsync();
    Task<IntegrationSettingsModel> UpdateIntegrationSettingsAsync(IntegrationSettingsModel settings);
    void ClearCache(); // Novo: limpar cache local
    event Action? OnSettingsChanged;
}
