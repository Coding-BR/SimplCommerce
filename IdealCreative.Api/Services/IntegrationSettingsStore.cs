using System.Text.Json;
using IdealCreative.Api.Contracts;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Services;

public sealed class IntegrationSettingsStore(
    AppDbContext db,
    IDataProtectionProvider protectionProvider,
    IConfiguration configuration)
{
    private const string SettingKey = "integrations-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector protector = protectionProvider.CreateProtector("IdealCreative.Integrations.v1");

    public async Task<IntegrationRuntimeSettings> GetRuntimeAsync(CancellationToken ct = default)
    {
        var stored = await LoadAsync(ct);
        return new IntegrationRuntimeSettings(
            new PayPalRuntimeSettings(
                stored.PayPal.Enabled,
                stored.PayPal.Sandbox,
                First(stored.PayPal.ClientId, configuration["Payments:PayPal:ClientId"]),
                Secret(stored.PayPal.Secret, configuration["Payments:PayPal:Secret"]),
                Secret(stored.PayPal.WebhookSecret, configuration["Payments:PayPal:WebhookSecret"])),
            new MercadoPagoRuntimeSettings(
                stored.MercadoPago.Enabled,
                stored.MercadoPago.Sandbox,
                Secret(stored.MercadoPago.AccessToken, configuration["Payments:MercadoPago:AccessToken"]),
                Secret(stored.MercadoPago.WebhookSecret, configuration["Payments:MercadoPago:WebhookSecret"])),
            new ShippingRuntimeSettings(
                stored.Shipping.Enabled,
                stored.Shipping.LocalPickupEnabled,
                stored.Shipping.PickupAddress,
                stored.Shipping.PickupInstructions,
                stored.Shipping.PickupPreparationDays,
                First(stored.Shipping.Provider, "Local"),
                First(stored.Shipping.OriginZipCode, configuration["Shipping:OriginZipCode"]),
                Secret(stored.Shipping.ApiToken, configuration["Shipping:ApiToken"]),
                stored.Shipping.BasePrice,
                stored.Shipping.PricePerKg,
                stored.Shipping.MaxAdditionalPrice,
                stored.Shipping.ExpressAdditionalPrice,
                stored.Shipping.EconomyDeliveryDays,
                stored.Shipping.ExpressDeliveryDays),
            new SmtpRuntimeSettings(
                stored.Smtp.Enabled,
                First(stored.Smtp.Host, configuration["Email:Smtp:Host"]),
                stored.Smtp.Port > 0 ? stored.Smtp.Port : GetInt("Email:Smtp:Port", 587),
                stored.Smtp.HasSavedValues ? stored.Smtp.UseSsl : GetBool("Email:Smtp:UseSsl", true),
                First(stored.Smtp.Username, configuration["Email:Smtp:Username"]),
                Secret(stored.Smtp.Password, configuration["Email:Smtp:Password"]),
                First(stored.Smtp.FromEmail, configuration["Email:Smtp:FromEmail"]),
                First(stored.Smtp.FromName, configuration["Email:Smtp:FromName"], "IdealCreative"),
                GetInt("Email:Smtp:TimeoutMilliseconds", 15_000)),
            new StorageRuntimeSettings(
                First(stored.Storage.Endpoint, configuration["Storage:Endpoint"], "minio:9000"),
                First(stored.Storage.Bucket, configuration["Storage:Bucket"], "idealcreative"),
                First(stored.Storage.PublicBaseUrl, configuration["Storage:PublicBaseUrl"], "http://localhost:5288/api/storage/public"),
                stored.Storage.HasSavedValues ? stored.Storage.UseSsl : GetBool("Storage:UseSsl", false),
                Secret(stored.Storage.AccessKey, configuration["Storage:AccessKey"]),
                Secret(stored.Storage.SecretKey, configuration["Storage:SecretKey"])));
    }

    public async Task<IntegrationSettingsResponse> GetAdminViewAsync(CancellationToken ct = default)
    {
        var runtime = await GetRuntimeAsync(ct);
        var row = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == SettingKey, ct);
        return ToView(runtime, row?.UpdatedAt);
    }

    public async Task<IntegrationSettingsResponse> SaveAsync(IntegrationSettingsUpdateRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var stored = await LoadAsync(ct);
        stored.PayPal = new StoredPayPal
        {
            Enabled = request.PayPal.Enabled,
            Sandbox = request.PayPal.Sandbox,
            ClientId = request.PayPal.ClientId.Trim(),
            Secret = UpdateSecret(stored.PayPal.Secret, request.PayPal.Secret, request.PayPal.ClearSecret),
            WebhookSecret = UpdateSecret(stored.PayPal.WebhookSecret, request.PayPal.WebhookSecret, request.PayPal.ClearWebhookSecret)
        };
        stored.MercadoPago = new StoredMercadoPago
        {
            Enabled = request.MercadoPago.Enabled,
            Sandbox = request.MercadoPago.Sandbox,
            AccessToken = UpdateSecret(stored.MercadoPago.AccessToken, request.MercadoPago.AccessToken, request.MercadoPago.ClearAccessToken),
            WebhookSecret = UpdateSecret(stored.MercadoPago.WebhookSecret, request.MercadoPago.WebhookSecret, request.MercadoPago.ClearWebhookSecret)
        };
        stored.Shipping = new StoredShipping
        {
            Enabled = request.Shipping.Enabled,
            LocalPickupEnabled = request.Shipping.LocalPickupEnabled,
            PickupAddress = request.Shipping.PickupAddress.Trim(),
            PickupInstructions = request.Shipping.PickupInstructions.Trim(),
            PickupPreparationDays = request.Shipping.PickupPreparationDays,
            Provider = request.Shipping.Provider.Trim(),
            OriginZipCode = request.Shipping.OriginZipCode.Trim(),
            ApiToken = UpdateSecret(stored.Shipping.ApiToken, request.Shipping.ApiToken, request.Shipping.ClearApiToken),
            BasePrice = request.Shipping.BasePrice,
            PricePerKg = request.Shipping.PricePerKg,
            MaxAdditionalPrice = request.Shipping.MaxAdditionalPrice,
            ExpressAdditionalPrice = request.Shipping.ExpressAdditionalPrice,
            EconomyDeliveryDays = request.Shipping.EconomyDeliveryDays,
            ExpressDeliveryDays = request.Shipping.ExpressDeliveryDays
        };
        stored.Smtp = new StoredSmtp
        {
            HasSavedValues = true,
            Enabled = request.Smtp.Enabled,
            Host = request.Smtp.Host.Trim(),
            Port = request.Smtp.Port,
            UseSsl = request.Smtp.UseSsl,
            Username = request.Smtp.Username.Trim(),
            Password = UpdateSecret(stored.Smtp.Password, request.Smtp.Password, request.Smtp.ClearPassword),
            FromEmail = request.Smtp.FromEmail.Trim(),
            FromName = request.Smtp.FromName.Trim()
        };
        stored.Storage = new StoredStorage
        {
            HasSavedValues = true,
            Endpoint = request.Storage.Endpoint.Trim(),
            Bucket = request.Storage.Bucket.Trim(),
            PublicBaseUrl = request.Storage.PublicBaseUrl.Trim(),
            UseSsl = request.Storage.UseSsl || IsHttpsEndpoint(request.Storage.Endpoint),
            AccessKey = UpdateSecret(stored.Storage.AccessKey, request.Storage.AccessKey, request.Storage.ClearAccessKey),
            SecretKey = UpdateSecret(stored.Storage.SecretKey, request.Storage.SecretKey, request.Storage.ClearSecretKey)
        };

        var row = await db.AppSettings.SingleOrDefaultAsync(x => x.Key == SettingKey, ct);
        if (row is null)
        {
            row = new AppSettingRecord { Key = SettingKey };
            db.AppSettings.Add(row);
        }
        row.ValueJson = JsonSerializer.Serialize(stored, JsonOptions);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToView(await GetRuntimeAsync(ct), row.UpdatedAt);
    }

    private async Task<StoredIntegrations> LoadAsync(CancellationToken ct)
    {
        var row = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == SettingKey, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.ValueJson)) return new StoredIntegrations();
        try { return JsonSerializer.Deserialize<StoredIntegrations>(row.ValueJson, JsonOptions) ?? new StoredIntegrations(); }
        catch (JsonException) { return new StoredIntegrations(); }
    }

    private string? UpdateSecret(string? current, string? replacement, bool clear)
    {
        if (clear) return Protect(string.Empty);
        return string.IsNullOrWhiteSpace(replacement) ? current : Protect(replacement.Trim());
    }

    private string Secret(string? protectedValue, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(protectedValue)) return fallback ?? string.Empty;
        var decrypted = Unprotect(protectedValue);
        return string.IsNullOrWhiteSpace(decrypted) ? (fallback ?? string.Empty) : decrypted;
    }

    private string Protect(string clearText)
    {
        if (string.IsNullOrEmpty(clearText)) return string.Empty;
        var key = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "IdealCreative.Integrations.Key.2026"));
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var bytes = System.Text.Encoding.UTF8.GetBytes(clearText);
        var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return "enc:" + Convert.ToBase64String(result);
    }

    private string Unprotect(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText)) return string.Empty;
        if (!cipherText.StartsWith("enc:"))
        {
            // Legacy DataProtector fallback
            try { return protector.Unprotect(cipherText); } catch { return cipherText; }
        }
        try
        {
            var raw = Convert.FromBase64String(cipherText[4..]);
            var key = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "IdealCreative.Integrations.Key.2026"));
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = key;
            var iv = new byte[16];
            Buffer.BlockCopy(raw, 0, iv, 0, 16);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(raw, 16, raw.Length - 16);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }


    private static IntegrationSettingsResponse ToView(IntegrationRuntimeSettings value, DateTimeOffset? updatedAt) => new()
    {
        UpdatedAt = updatedAt,
        PayPal = new PayPalIntegrationView { Enabled = value.PayPal.Enabled, Sandbox = value.PayPal.Sandbox, ClientId = value.PayPal.ClientId, SecretConfigured = Has(value.PayPal.Secret), WebhookSecretConfigured = Has(value.PayPal.WebhookSecret) },
        MercadoPago = new MercadoPagoIntegrationView { Enabled = value.MercadoPago.Enabled, Sandbox = value.MercadoPago.Sandbox, AccessTokenConfigured = Has(value.MercadoPago.AccessToken), WebhookSecretConfigured = Has(value.MercadoPago.WebhookSecret) },
        Shipping = new ShippingIntegrationView { Enabled = value.Shipping.Enabled, LocalPickupEnabled = value.Shipping.LocalPickupEnabled, PickupAddress = value.Shipping.PickupAddress, PickupInstructions = value.Shipping.PickupInstructions, PickupPreparationDays = value.Shipping.PickupPreparationDays, Provider = value.Shipping.Provider, OriginZipCode = value.Shipping.OriginZipCode, ApiTokenConfigured = Has(value.Shipping.ApiToken), BasePrice = value.Shipping.BasePrice, PricePerKg = value.Shipping.PricePerKg, MaxAdditionalPrice = value.Shipping.MaxAdditionalPrice, ExpressAdditionalPrice = value.Shipping.ExpressAdditionalPrice, EconomyDeliveryDays = value.Shipping.EconomyDeliveryDays, ExpressDeliveryDays = value.Shipping.ExpressDeliveryDays },
        Smtp = new SmtpIntegrationView { Enabled = value.Smtp.Enabled, Host = value.Smtp.Host, Port = value.Smtp.Port, UseSsl = value.Smtp.UseSsl, Username = value.Smtp.Username, PasswordConfigured = Has(value.Smtp.Password), FromEmail = value.Smtp.FromEmail, FromName = value.Smtp.FromName },
        Storage = new StorageIntegrationView { Endpoint = value.Storage.Endpoint, Bucket = value.Storage.Bucket, PublicBaseUrl = value.Storage.PublicBaseUrl, UseSsl = value.Storage.UseSsl, AccessKeyConfigured = Has(value.Storage.AccessKey), SecretKeyConfigured = Has(value.Storage.SecretKey) }
    };

    private static void Validate(IntegrationSettingsUpdateRequest request)
    {
        if (request.Smtp.Port is < 1 or > 65535) throw new ArgumentException("A porta SMTP deve estar entre 1 e 65535.");
        if (request.Smtp.Enabled && (string.IsNullOrWhiteSpace(request.Smtp.Host) || string.IsNullOrWhiteSpace(request.Smtp.FromEmail))) throw new ArgumentException("SMTP ativo exige servidor e e-mail remetente.");
        if (!string.IsNullOrWhiteSpace(request.Smtp.FromEmail) && !request.Smtp.FromEmail.Contains('@')) throw new ArgumentException("O e-mail remetente SMTP é inválido.");
        if (request.Shipping.BasePrice < 0 || request.Shipping.PricePerKg < 0 || request.Shipping.MaxAdditionalPrice < 0 || request.Shipping.ExpressAdditionalPrice < 0) throw new ArgumentException("Os valores de frete não podem ser negativos.");
        if (request.Shipping.EconomyDeliveryDays is < 1 or > 60 || request.Shipping.ExpressDeliveryDays is < 1 or > 60) throw new ArgumentException("Os prazos de frete devem estar entre 1 e 60 dias.");
        if (request.Shipping.PickupPreparationDays is < 0 or > 60) throw new ArgumentException("O prazo para retirada deve estar entre 0 e 60 dias.");
        if (!request.Shipping.Enabled && !request.Shipping.LocalPickupEnabled) throw new ArgumentException("Ative o cálculo de frete ou a retirada local para produtos físicos.");
        if (request.Shipping.LocalPickupEnabled && string.IsNullOrWhiteSpace(request.Shipping.PickupAddress)) throw new ArgumentException("Informe o endereço da retirada local.");
        if (!new[] { "Local", "MelhorEnvio" }.Contains(request.Shipping.Provider, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Provedor de frete inválido.");
        if (string.IsNullOrWhiteSpace(request.Storage.Endpoint) || string.IsNullOrWhiteSpace(request.Storage.Bucket)) throw new ArgumentException("Storage exige endpoint e bucket.");
        if (!string.IsNullOrWhiteSpace(request.Storage.PublicBaseUrl) && !Uri.TryCreate(request.Storage.PublicBaseUrl, UriKind.Absolute, out _)) throw new ArgumentException("A URL pública do storage é inválida.");
    }

    private int GetInt(string key, int fallback) => int.TryParse(configuration[key], out var value) ? value : fallback;
    private bool GetBool(string key, bool fallback) => bool.TryParse(configuration[key], out var value) ? value : fallback;
    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool IsHttpsEndpoint(string? value) => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    private static string First(params string?[] values) => values.FirstOrDefault(Has) ?? string.Empty;

    private sealed class StoredIntegrations
    {
        public StoredPayPal PayPal { get; set; } = new();
        public StoredMercadoPago MercadoPago { get; set; } = new();
        public StoredShipping Shipping { get; set; } = new();
        public StoredSmtp Smtp { get; set; } = new();
        public StoredStorage Storage { get; set; } = new();
    }
    private sealed class StoredPayPal { public bool Enabled { get; set; } public bool Sandbox { get; set; } = true; public string ClientId { get; set; } = string.Empty; public string? Secret { get; set; } public string? WebhookSecret { get; set; } }
    private sealed class StoredMercadoPago { public bool Enabled { get; set; } public bool Sandbox { get; set; } = true; public string? AccessToken { get; set; } public string? WebhookSecret { get; set; } }
    private sealed class StoredShipping { public bool Enabled { get; set; } = true; public bool LocalPickupEnabled { get; set; } = true; public string PickupAddress { get; set; } = "Local informado na confirmação do pedido"; public string PickupInstructions { get; set; } = "Aguarde a confirmação de que o pedido está pronto."; public int PickupPreparationDays { get; set; } = 1; public string Provider { get; set; } = "Local"; public string OriginZipCode { get; set; } = string.Empty; public string? ApiToken { get; set; } public decimal BasePrice { get; set; } = 18m; public decimal PricePerKg { get; set; } = 4m; public decimal MaxAdditionalPrice { get; set; } = 45m; public decimal ExpressAdditionalPrice { get; set; } = 18m; public int EconomyDeliveryDays { get; set; } = 7; public int ExpressDeliveryDays { get; set; } = 3; }
    private sealed class StoredSmtp { public bool HasSavedValues { get; set; } public bool Enabled { get; set; } = true; public string Host { get; set; } = string.Empty; public int Port { get; set; } public bool UseSsl { get; set; } = true; public string Username { get; set; } = string.Empty; public string? Password { get; set; } public string FromEmail { get; set; } = string.Empty; public string FromName { get; set; } = string.Empty; }
    private sealed class StoredStorage { public bool HasSavedValues { get; set; } public string Endpoint { get; set; } = string.Empty; public string Bucket { get; set; } = string.Empty; public string PublicBaseUrl { get; set; } = string.Empty; public bool UseSsl { get; set; } = true; public string? AccessKey { get; set; } public string? SecretKey { get; set; } }
}

public sealed record IntegrationRuntimeSettings(PayPalRuntimeSettings PayPal, MercadoPagoRuntimeSettings MercadoPago, ShippingRuntimeSettings Shipping, SmtpRuntimeSettings Smtp, StorageRuntimeSettings Storage);
public sealed record PayPalRuntimeSettings(bool Enabled, bool Sandbox, string ClientId, string Secret, string WebhookSecret) { public string BaseUrl => Sandbox ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com"; }
public sealed record MercadoPagoRuntimeSettings(bool Enabled, bool Sandbox, string AccessToken, string WebhookSecret);
public sealed record ShippingRuntimeSettings(bool Enabled, bool LocalPickupEnabled, string PickupAddress, string PickupInstructions, int PickupPreparationDays, string Provider, string OriginZipCode, string ApiToken, decimal BasePrice, decimal PricePerKg, decimal MaxAdditionalPrice, decimal ExpressAdditionalPrice, int EconomyDeliveryDays, int ExpressDeliveryDays);
public sealed record SmtpRuntimeSettings(bool Enabled, string Host, int Port, bool UseSsl, string Username, string Password, string FromEmail, string FromName, int TimeoutMilliseconds);
public sealed record StorageRuntimeSettings(string Endpoint, string Bucket, string PublicBaseUrl, bool UseSsl, string AccessKey, string SecretKey);
