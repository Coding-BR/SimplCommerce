namespace BlazorClient.Models;

public sealed class IntegrationSettingsModel
{
    public PayPalIntegrationModel PayPal { get; set; } = new();
    public MercadoPagoIntegrationModel MercadoPago { get; set; } = new();
    public ShippingIntegrationModel Shipping { get; set; } = new();
    public SmtpIntegrationModel Smtp { get; set; } = new();
    public StorageIntegrationModel Storage { get; set; } = new();
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class PayPalIntegrationModel
{
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public string ClientId { get; set; } = string.Empty;
    public bool SecretConfigured { get; set; }
    public string? Secret { get; set; }
    public bool WebhookSecretConfigured { get; set; }
    public string? WebhookSecret { get; set; }
    public bool ClearSecret { get; set; }
    public bool ClearWebhookSecret { get; set; }
}

public sealed class MercadoPagoIntegrationModel
{
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public bool AccessTokenConfigured { get; set; }
    public string? AccessToken { get; set; }
    public bool WebhookSecretConfigured { get; set; }
    public string? WebhookSecret { get; set; }
    public bool ClearAccessToken { get; set; }
    public bool ClearWebhookSecret { get; set; }
}

public sealed class ShippingIntegrationModel
{
    public bool Enabled { get; set; } = true;
    public bool LocalPickupEnabled { get; set; } = true;
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupInstructions { get; set; } = string.Empty;
    public int PickupPreparationDays { get; set; } = 1;
    public string Provider { get; set; } = "Local";
    public string OriginZipCode { get; set; } = string.Empty;
    public bool ApiTokenConfigured { get; set; }
    public string? ApiToken { get; set; }
    public bool ClearApiToken { get; set; }
    public decimal BasePrice { get; set; } = 18m;
    public decimal PricePerKg { get; set; } = 4m;
    public decimal MaxAdditionalPrice { get; set; } = 45m;
    public decimal ExpressAdditionalPrice { get; set; } = 18m;
    public int EconomyDeliveryDays { get; set; } = 7;
    public int ExpressDeliveryDays { get; set; } = 3;
}

public sealed class SmtpIntegrationModel
{
    public bool Enabled { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public bool PasswordConfigured { get; set; }
    public string? Password { get; set; }
    public bool ClearPassword { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "IdealCreative";
}

public sealed class StorageIntegrationModel
{
    public string Endpoint { get; set; } = string.Empty;
    public string Bucket { get; set; } = "idealcreative";
    public string PublicBaseUrl { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public bool AccessKeyConfigured { get; set; }
    public string? AccessKey { get; set; }
    public bool SecretKeyConfigured { get; set; }
    public string? SecretKey { get; set; }
    public bool ClearAccessKey { get; set; }
    public bool ClearSecretKey { get; set; }
}
