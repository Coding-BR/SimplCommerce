namespace IdealCreative.Api.Contracts;

public sealed class IntegrationSettingsResponse
{
    public PayPalIntegrationView PayPal { get; init; } = new();
    public MercadoPagoIntegrationView MercadoPago { get; init; } = new();
    public ShippingIntegrationView Shipping { get; init; } = new();
    public SmtpIntegrationView Smtp { get; init; } = new();
    public StorageIntegrationView Storage { get; init; } = new();
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class PayPalIntegrationView
{
    public bool Enabled { get; init; }
    public bool Sandbox { get; init; } = true;
    public string ClientId { get; init; } = string.Empty;
    public bool SecretConfigured { get; init; }
    public bool WebhookSecretConfigured { get; init; }
}

public sealed class MercadoPagoIntegrationView
{
    public bool Enabled { get; init; }
    public bool Sandbox { get; init; } = true;
    public bool AccessTokenConfigured { get; init; }
    public bool WebhookSecretConfigured { get; init; }
}

public sealed class ShippingIntegrationView
{
    public bool Enabled { get; init; } = true;
    public bool LocalPickupEnabled { get; init; } = true;
    public string PickupAddress { get; init; } = string.Empty;
    public string PickupInstructions { get; init; } = string.Empty;
    public int PickupPreparationDays { get; init; } = 1;
    public string Provider { get; init; } = "Local";
    public string OriginZipCode { get; init; } = string.Empty;
    public bool ApiTokenConfigured { get; init; }
    public decimal BasePrice { get; init; } = 18m;
    public decimal PricePerKg { get; init; } = 4m;
    public decimal MaxAdditionalPrice { get; init; } = 45m;
    public decimal ExpressAdditionalPrice { get; init; } = 18m;
    public int EconomyDeliveryDays { get; init; } = 7;
    public int ExpressDeliveryDays { get; init; } = 3;
}

public sealed class SmtpIntegrationView
{
    public bool Enabled { get; init; } = true;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public bool PasswordConfigured { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "IdealCreative";
}

public sealed class StorageIntegrationView
{
    public string Endpoint { get; init; } = string.Empty;
    public string Bucket { get; init; } = "idealcreative";
    public string PublicBaseUrl { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public bool AccessKeyConfigured { get; init; }
    public bool SecretKeyConfigured { get; init; }
}

public sealed class IntegrationSettingsUpdateRequest
{
    public PayPalIntegrationUpdate PayPal { get; init; } = new();
    public MercadoPagoIntegrationUpdate MercadoPago { get; init; } = new();
    public ShippingIntegrationUpdate Shipping { get; init; } = new();
    public SmtpIntegrationUpdate Smtp { get; init; } = new();
    public StorageIntegrationUpdate Storage { get; init; } = new();
}

public sealed class PayPalIntegrationUpdate
{
    public bool Enabled { get; init; }
    public bool Sandbox { get; init; } = true;
    public string ClientId { get; init; } = string.Empty;
    public string? Secret { get; init; }
    public string? WebhookSecret { get; init; }
    public bool ClearSecret { get; init; }
    public bool ClearWebhookSecret { get; init; }
}

public sealed class MercadoPagoIntegrationUpdate
{
    public bool Enabled { get; init; }
    public bool Sandbox { get; init; } = true;
    public string? AccessToken { get; init; }
    public string? WebhookSecret { get; init; }
    public bool ClearAccessToken { get; init; }
    public bool ClearWebhookSecret { get; init; }
}

public sealed class ShippingIntegrationUpdate
{
    public bool Enabled { get; init; } = true;
    public bool LocalPickupEnabled { get; init; } = true;
    public string PickupAddress { get; init; } = string.Empty;
    public string PickupInstructions { get; init; } = string.Empty;
    public int PickupPreparationDays { get; init; } = 1;
    public string Provider { get; init; } = "Local";
    public string OriginZipCode { get; init; } = string.Empty;
    public string? ApiToken { get; init; }
    public bool ClearApiToken { get; init; }
    public decimal BasePrice { get; init; } = 18m;
    public decimal PricePerKg { get; init; } = 4m;
    public decimal MaxAdditionalPrice { get; init; } = 45m;
    public decimal ExpressAdditionalPrice { get; init; } = 18m;
    public int EconomyDeliveryDays { get; init; } = 7;
    public int ExpressDeliveryDays { get; init; } = 3;
}

public sealed class SmtpIntegrationUpdate
{
    public bool Enabled { get; init; } = true;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string? Password { get; init; }
    public bool ClearPassword { get; init; }
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "IdealCreative";
}

public sealed class StorageIntegrationUpdate
{
    public string Endpoint { get; init; } = string.Empty;
    public string Bucket { get; init; } = "idealcreative";
    public string PublicBaseUrl { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
    public bool ClearAccessKey { get; init; }
    public bool ClearSecretKey { get; init; }
}
