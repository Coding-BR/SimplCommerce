using Minio;

namespace IdealCreative.Api.Services;

public interface IStorageClientFactory
{
    IMinioClient Create(StorageRuntimeSettings settings);
}

public sealed class StorageClientFactory : IStorageClientFactory
{
    public IMinioClient Create(StorageRuntimeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey))
            throw new InvalidOperationException("O armazenamento R2/MinIO não está completamente configurado.");

        var configuredEndpoint = settings.Endpoint.Trim();
        var endpoint = configuredEndpoint;
        var useSsl = settings.UseSsl;
        if (Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var uri))
        {
            endpoint = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            useSsl |= string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        return new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(useSsl)
            .Build();
    }
}
