using System.Globalization;
using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface ILocalizationService
{
    event Action? OnLanguageChanged;
    string CurrentLanguage { get; }
    CultureInfo CurrentCulture { get; }
    Task InitializeAsync();
    string Get(string key, params object[]? args);
    Task SetLanguageAsync(string languageCode);
    string FormatCurrency(decimal value);
    string FormatCurrency(double value);
    string FormatCurrency(decimal? value);
    string FormatNumber(decimal value, int decimals = 2);
    string FormatDate(DateTime date);
    string FormatDate(DateTime? date);
    string FormatDateLong(DateTime date);
    string FormatTime(DateTime time);
    string FormatTime(DateTime? time);
    string FormatDateTime(DateTime dateTime);
    string FormatDateTime(DateTime? dateTime);
    string FormatDateTimeLong(DateTime dateTime);
    string FormatDateTimeLong(DateTime? dateTime);
}
