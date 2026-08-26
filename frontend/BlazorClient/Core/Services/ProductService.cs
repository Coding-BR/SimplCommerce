using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorClient.Core.Interfaces;
using BlazorClient.Models;

namespace BlazorClient.Core.Services;

public class ProductService : IProductService
{
    private readonly IHttpClientFactory _factory;
    private readonly IClientLocaleService _localeService;
    private readonly ILocalizationService _localizationService;
    
    // Simple in-memory cache
    private readonly Dictionary<string, (object Data, DateTime Expiration)> _cache = new();

    public ProductService(IHttpClientFactory factory, IClientLocaleService localeService, ILocalizationService localizationService)
    {
        _factory = factory;
        _localeService = localeService;
        _localizationService = localizationService;
    }

    private HttpClient PublicClient => _factory.CreateClient("PublicAPI");
    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");

    private T? GetFromCache<T>(string key)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            if (item.Expiration > DateTime.Now)
            {
                return (T)item.Data;
            }
            _cache.Remove(key); // Expired
        }
        return default;
    }

    private void SetCache<T>(string key, T data, TimeSpan duration)
    {
        if (data != null)
        {
            _cache[key] = (data, DateTime.Now.Add(duration));
        }
    }

    private void InvalidateCache(string keyPrefix = "")
    {
        if (string.IsNullOrEmpty(keyPrefix))
        {
            _cache.Clear();
            return;
        }

        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(keyPrefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
        }
    }

    public async Task<List<Tag>> GetTagsAsync(bool forceRefresh = false)
    {
        // OPTIMIZED: Cache tags with translations for 30 minutes
        var cacheKey = "tags_v2";
        
        if (!forceRefresh)
        {
            var cached = GetFromCache<List<Tag>>(cacheKey);
            if (cached != null) return cached;
        }

        try {
            var url = "api/tags";
            if (forceRefresh) url += "?refresh=true"; // Matches commonly used pattern, check Controller next

            var tags = await PublicClient.GetFromJsonAsync<List<Tag>>(url) 
                   ?? new List<Tag>();
            
            SetCache(cacheKey, tags, TimeSpan.FromMinutes(30));
            return tags;
        } catch (Exception ex) {
            Console.WriteLine($"Error fetching tags: {ex.Message}");
            return new List<Tag>();
        }
    }

    public async Task<(Tag? Tag, string? Error)> CreateTagAsync(CreateTagDto dto)
    {
        try
        {
            var response = await AuthClient.PostAsJsonAsync("api/tags", dto);
            if (response.IsSuccessStatusCode)
            {
                var tag = await response.Content.ReadFromJsonAsync<Tag>();
                InvalidateCache("tags_"); // Clear tag caches
                return (tag, null);
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error creating tag: {response.StatusCode} - {err}");
                // Try to parse json error
                try {
                    var errObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(err);
                    if (errObj != null && errObj.ContainsKey("error")) return (null, errObj["error"]);
                } catch { }
                
                return (null, $"Erro {response.StatusCode}: {err}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating tag: {ex.Message}");
            return (null, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateTagAsync(string id, UpdateTagDto dto)
    {
        try
        {
            var response = await AuthClient.PutAsJsonAsync($"api/tags/{id}", dto);
            if (response.IsSuccessStatusCode)
            {
                InvalidateCache("tags_");
                return (true, null);
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error updating tag: {response.StatusCode} - {err}");
                return (false, err);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating tag: {ex.Message}");
            return (false, ex.Message);
        }
    }

    public async Task<bool> DeleteTagAsync(string id)
    {
        try
        {
            var response = await AuthClient.DeleteAsync($"api/tags/{id}");
            if (response.IsSuccessStatusCode)
            {
                InvalidateCache("tags_");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting tag: {ex.Message}");
            return false;
        }
    }

    public async Task<ProductListResponse> GetProductsAsync(
        int page = 1, 
        int pageSize = 10, 
        string? categoryId = null, 
        string? orderBy = null, 
        string? tag = null, 
        string? lastDocId = null, 
        string? type = null, 
        bool? onlyOutOfStock = null, 
        string? currency = null, 
        string? search = null, 
        string? language = null, 
        decimal? minPrice = null, 
        decimal? maxPrice = null,
        bool includeUnpublished = false)
    {
        // Clean parameters
        currency = currency?.ToUpper();
        
        var cacheKey = $"products_v2_{page}_{pageSize}_{categoryId}_{orderBy}_{tag}_{lastDocId}_{type}_{onlyOutOfStock}_{currency}_{language}_{minPrice}_{maxPrice}_{search}_{includeUnpublished}";
        
        // Cache read-heavy pages for 5 mins, others for 1 min
        var isMainPage = page == 1 && string.IsNullOrEmpty(categoryId) && string.IsNullOrEmpty(tag) && string.IsNullOrEmpty(type) && onlyOutOfStock == null;
        
        var cached = GetFromCache<ProductListResponse>(cacheKey);
        if (cached != null) return cached;

        // Use a list to build parameters cleanly and avoid duplications
        var parameters = new List<string>();
        parameters.Add($"page={page}");
        parameters.Add($"pageSize={pageSize}");

        if (!string.IsNullOrEmpty(categoryId)) parameters.Add($"categoryId={categoryId}");
        if (!string.IsNullOrEmpty(orderBy)) parameters.Add($"orderBy={orderBy}");
        if (!string.IsNullOrEmpty(tag)) parameters.Add($"tag={Uri.EscapeDataString(tag)}");
        if (!string.IsNullOrEmpty(lastDocId)) parameters.Add($"lastDocId={lastDocId}");
        if (!string.IsNullOrEmpty(type)) parameters.Add($"type={type}");
        if (onlyOutOfStock == true) parameters.Add("onlyOutOfStock=true");
        if (!string.IsNullOrEmpty(currency)) parameters.Add($"currency={currency}");
        if (!string.IsNullOrEmpty(language)) parameters.Add($"language={language}");
        if (minPrice.HasValue) parameters.Add($"minPrice={minPrice}");
        if (maxPrice.HasValue) parameters.Add($"maxPrice={maxPrice}");
        if (!string.IsNullOrEmpty(search)) parameters.Add($"search={Uri.EscapeDataString(search)}");
        if (includeUnpublished) parameters.Add("includeUnpublished=true");
        
        // Add timestamp to prevent browser caching (HTTP 304/Disk Cache)
        parameters.Add($"t={DateTime.UtcNow.Ticks}");

        var url = $"api/products?{string.Join("&", parameters)}";

        var result = await (includeUnpublished ? AuthClient : PublicClient).GetFromJsonAsync<ProductListResponse>(url)
               ?? new ProductListResponse();

        SetCache(cacheKey, result, isMainPage ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1));
        return result;
    }

    public async Task<List<Product>> GetBestSellingProductsAsync(int count = 8, string? currency = null)
    {
        var language = _localeService.CurrentLocale?.Language;
        // This calls GetProductsAsync internally, so it benefits from that cache or we can cache this specifically
        return (await GetProductsAsync(1, count, orderBy: "sales", currency: currency, language: language)).Items;
    }

    public async Task<ProductListResponse> GetFeaturedProductsAsync(string? currency = null)
    {
        var language = _localeService.CurrentLocale?.Language;
        var cacheKey = $"featured_{currency}_{language}";
        var cached = GetFromCache<ProductListResponse>(cacheKey);
        if (cached != null) return cached;

        var url = "api/products/featured";
        var separator = "?";
        if (!string.IsNullOrEmpty(currency))
        {
            url += $"{separator}currency={currency}";
            separator = "&";
        }
        if (!string.IsNullOrEmpty(language))
        {
            url += $"{separator}language={language}";
        }

        var result = await PublicClient.GetFromJsonAsync<ProductListResponse>(url) 
               ?? new ProductListResponse();
        
        SetCache(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<Product?> GetProductAsync(string id, string? currency = null)
    {
        // OPTIMIZED: Cache by ID and currency only (not language)
        // Product contains all translations in Translations field
        // Frontend applies translation based on user's selected language
        var cacheKey = $"product_{id}_{currency}";
        var cached = GetFromCache<Product>(cacheKey);
        if (cached != null) return cached;

        try 
        {
            var url = $"api/products/{id}?";
            if (!string.IsNullOrEmpty(currency))
            {
                url += $"currency={currency}&";
            }
            url += $"t={DateTime.UtcNow.Ticks}";
            
            var product = await PublicClient.GetFromJsonAsync<Product>(url);
            
            if (product != null)
            {
                SetCache(cacheKey, product, TimeSpan.FromMinutes(5));
            }
            return product;
        }
        catch 
        {
            return null;
        }
    }

    public async Task<Product?> GetAdminProductAsync(string id)
    {
        try 
        {
            // Use AuthClient (Authorization header avoids most caches)
            // And add timestamp just to be sure
            var url = $"api/products/{id}?t={DateTime.UtcNow.Ticks}";
            return await AuthClient.GetFromJsonAsync<Product>(url);
        }
        catch 
        {
            return null;
        }
    }

    public async Task<Product?> CreateProductAsync(CreateProductDto dto)
    {
        var response = await AuthClient.PostAsJsonAsync("api/products", dto);
        if (response.IsSuccessStatusCode)
        {
            var product = await response.Content.ReadFromJsonAsync<Product>();
            InvalidateCache(); // Clear EVERYTHING to be safe
            return product;
        }
        
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"CreateProduct failed: {response.StatusCode} - {error}");
        return null;
    }

    public async Task<bool> UpdateProductAsync(string id, CreateProductDto dto)
    {
        var updateDto = new UpdateProductDto
        {
            Id = id,
            Title = dto.Title,
            Price = dto.Price,
            Description = dto.Description,
            FullDesc = dto.FullDesc,

            Qty = dto.Qty,
            ImageName = dto.ImageName,
            Images = dto.Images,
            CategoryId = dto.CategoryId,
            VideoUrl = dto.VideoUrl,
            DownloadUrl = dto.DownloadUrl,
            Tags = dto.Tags,
            IsSubscription = dto.IsSubscription,
            PayPalPlanId = dto.PayPalPlanId,
            RecurringInterval = dto.RecurringInterval,
            DurationMonths = dto.DurationMonths,
            IsDigital = dto.IsDigital,
            DigitalFilePath = dto.DigitalFilePath,
            HideDigitalFromCustomer = dto.HideDigitalFromCustomer,
            TelegramGroupId = dto.TelegramGroupId,
            
            // Shipping Dimensions
            Weight = dto.Weight,
            Width = dto.Width,
            Height = dto.Height,
            Length = dto.Length,

            // Fiscal Fields
            Ncm = dto.Ncm,
            Cest = dto.Cest,
            Origem = dto.Origem,
            Gtin = dto.Gtin,
            Unidade = dto.Unidade,
            Translations = dto.Translations
        };
        var response = await AuthClient.PutAsJsonAsync($"api/products/{id}", updateDto);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache(); // Clear EVERYTHING
            return true;
        }
        return false;
    }

    public async Task<bool> UpdateProductStatsAsync(string id, BlazorClient.Models.UpdateProductStatsDto dto)
    {
        var response = await AuthClient.PutAsJsonAsync($"api/products/{id}/stats", dto);
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache();
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        var response = await AuthClient.DeleteAsync($"api/products/{id}");
        if (response.IsSuccessStatusCode)
        {
            InvalidateCache(); // Clear EVERYTHING
            return true;
        }
        return false;
    }

    public async Task<string?> UploadProductImageAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, Action<int>? onProgress = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            // Keep this client-side guard aligned with the API limit.
            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Size > maxFileSize)
                throw new InvalidOperationException("A imagem deve ter no máximo 10 MB.");
            
            // Create a stream with progress tracking
            using var fileStream = file.OpenReadStream(maxAllowedSize: maxFileSize);
            var totalBytes = file.Size;
            
            var progressStream = new ProgressStream(fileStream, totalBytes, (progress) => 
            {
                onProgress?.Invoke(progress);
            });
            
            var fileContent = new StreamContent(progressStream);
            
            // Explicitly set content type, default to octet-stream if missing, but API expects image/*
            var contentType = string.IsNullOrEmpty(file.ContentType) ? "image/jpeg" : file.ContentType;
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            
            // "file" must match the parameter name in the Controller
            var fileName = file.Name;
            if (contentType == "image/webp" && !fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.ChangeExtension(fileName, ".webp");
            }
            content.Add(fileContent, "file", fileName);

            var response = await AuthClient.PostAsync("api/upload/product-image", content);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
                return result?.Url ?? throw new InvalidOperationException("O servidor não retornou o endereço da imagem.");
            }
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractUploadError(responseBody));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível enviar a imagem. Verifique a conexão e tente novamente.", ex);
        }
    }

    private static string ExtractUploadError(string responseBody)
    {
        try
        {
            var error = JsonSerializer.Deserialize<UploadErrorResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!string.IsNullOrWhiteSpace(error?.Message)) return error.Message;
        }
        catch (JsonException)
        {
            // Use the generic message below when the response is not JSON.
        }
        return "Não foi possível enviar a imagem. Confirme o formato e tente novamente.";
    }

    private sealed record UploadErrorResponse(string? Message);

    public async Task<bool> DeleteProductImageAsync(string imageUrl)
    {
        try
        {
            var response = await AuthClient.DeleteAsync($"api/upload/product-image?url={Uri.EscapeDataString(imageUrl)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting product image: {ex.Message}");
            return false;
        }
    }

    public async Task<List<Category>> GetCategoriesAsync(bool forceRefresh = false)
    {
        // OPTIMIZED: Cache categories for 30 minutes to avoid repeated API calls
        // Categories include ALL translations, so no need to vary by language
        var cacheKey = "categories_all";
        
        if (!forceRefresh)
        {
            var cached = GetFromCache<List<Category>>(cacheKey);
            if (cached != null) return cached;
        }

        var url = "api/categories/all";
        if (forceRefresh) url += "?refresh=true";

        var categories = await PublicClient.GetFromJsonAsync<List<Category>>(url) 
               ?? new List<Category>();
        
        // Cache for 30 minutes (categories rarely change)
        SetCache(cacheKey, categories, TimeSpan.FromMinutes(30));
        return categories;
    }

    public async Task<Category?> CreateCategoryAsync(CreateCategoryDto dto)
    {
        var response = await AuthClient.PostAsJsonAsync("api/categories", dto);
        if (response.IsSuccessStatusCode)
        {
            // Invalidate categories cache
            InvalidateCache("categories_");
            return await response.Content.ReadFromJsonAsync<Category>();
        }
        return null;
    }

    public async Task<bool> UpdateCategoryAsync(string id, UpdateCategoryDto dto)
    {
        var response = await AuthClient.PutAsJsonAsync($"api/categories/{id}", dto);
        if (response.IsSuccessStatusCode)
        {
            // Invalidate categories cache
            InvalidateCache("categories_");
            return true;
        }
        return false;
    }

    public async Task<bool> DeleteCategoryAsync(string id)
    {
        var response = await AuthClient.DeleteAsync($"api/categories/{id}");
        if (response.IsSuccessStatusCode)
        {
            // Invalidate categories cache
            InvalidateCache("categories_");
            return true;
        }
        return false;
    }

    public async Task<DigitalFileUploadResponse?> UploadDigitalFileAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, Action<int>? onProgress = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            // 100MB limit for digital files
            var maxFileSize = 100 * 1024 * 1024L;
            
            var fileStream = file.OpenReadStream(maxAllowedSize: maxFileSize);
            var totalBytes = file.Size;
            
            var progressStream = new ProgressStream(fileStream, totalBytes, (progress) => 
            {
                onProgress?.Invoke(progress);
            });
            
            var fileContent = new StreamContent(progressStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", file.Name);

            var response = await AuthClient.PostAsync("api/upload/digital-file", content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DigitalFileUploadResponse>();
            }
            Console.WriteLine($"Upload failed: {await response.Content.ReadAsStringAsync()}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading digital file: {ex.Message}");
            return null;
        }
    }

    public async Task<(DigitalFileUploadResponse? Response, string? Error)> UploadDigitalFileFromBytesAsync(byte[] fileBytes, string fileName, Action<int>? onProgress = null)
    {
        try
        {
            onProgress?.Invoke(0);
            using var form = new MultipartFormDataContent();
            using var bytes = new ByteArrayContent(fileBytes);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(bytes, "file", fileName);
            onProgress?.Invoke(20);
            var response = await AuthClient.PostAsync("api/upload/digital-file", form);
            if (!response.IsSuccessStatusCode) return (null, $"Falha no upload: {response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
            onProgress?.Invoke(100);
            return (await response.Content.ReadFromJsonAsync<DigitalFileUploadResponse>(), null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading digital file: {ex.Message}");
            return (null, $"Erro de conexão: {ex.Message}");
        }
    }

    public async Task<bool> DeleteDigitalFileAsync(string path)
    {
        try
        {
            var response = await AuthClient.DeleteAsync($"api/upload/digital-file?path={Uri.EscapeDataString(path)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting digital file: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveProductDigitalFileAsync(string productId)
    {
        try
        {
            var response = await AuthClient.DeleteAsync($"api/upload/digital-file/{productId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing product digital file: {ex.Message}");
            return false;
        }
    }

    public async Task<DownloadLinkResponse?> GetDownloadLinkAsync(string productId)
    {
        try
        {
            var response = await AuthClient.GetAsync($"api/downloads/{productId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DownloadLinkResponse>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting download link: {ex.Message}");
            return null;
        }
    }

    public async Task<DownloadLinkResponse?> GetAdminDownloadLinkAsync(string productId)
    {
        try
        {
            var response = await AuthClient.GetAsync($"api/downloads/{productId}/admin");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DownloadLinkResponse>(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting admin download link: {ex.Message}");
            return null;
        }
    }

    public async Task<DigitalDownloadListResponse> GetMyDownloadsAsync(int page = 1, int pageSize = 10, string? lastDocId = null)
    {
        try
        {
            var url = $"api/downloads?page={page}&pageSize={pageSize}&t={DateTime.UtcNow.Ticks}";
            if (!string.IsNullOrEmpty(lastDocId))
            {
                url += $"&lastDocId={lastDocId}";
            }

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await AuthClient.GetFromJsonAsync<DigitalDownloadListResponse>(url, options) 
                   ?? new DigitalDownloadListResponse();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting downloads: {ex.Message}");
            return new DigitalDownloadListResponse();
        }
    }

    public async Task<FileVersionDetails?> InspectFileVersionsAsync(string path)
    {
        try
        {
            return await AuthClient.GetFromJsonAsync<FileVersionDetails>($"api/storage/versions?path={Uri.EscapeDataString(path)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inspecting file versions: {ex.Message}");
            return null;
        }
    }

    public async Task<B2BrowserResponse?> BrowseFilesAsync(string path = "", int pageSize = 10, string? continuationToken = null, string? search = null)
    {
        try
        {
            var url = $"api/storage/browser?path={Uri.EscapeDataString(path)}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(continuationToken))
            {
                url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
            }
            if (!string.IsNullOrEmpty(search))
            {
                url += $"&search={Uri.EscapeDataString(search)}";
            }
            return await AuthClient.GetFromJsonAsync<B2BrowserResponse>(url);
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error browsing files: {ex.Message}");
             var errorResp = new B2BrowserResponse();
             errorResp.Items.Add(new B2FileItem { Name = $"CLIENT ERROR: {ex.Message}", IsFolder = false, Size = 0 });
             return errorResp;
        }
    }

    public async Task<string?> GetB2DownloadUrlAsync(string path)
    {
        try
        {
            var result = await AuthClient.GetFromJsonAsync<DownloadUrlResponse>($"api/storage/download?path={Uri.EscapeDataString(path)}");
            return result?.Url;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting download URL: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteB2FileAsync(string path)
    {
        try
        {
            var response = await AuthClient.DeleteAsync($"api/storage/file?path={Uri.EscapeDataString(path)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UploadFileToPathAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, string targetPath = "")
    {
        try
        {
            using var content = new MultipartFormDataContent();
            const long maxFileSize = 100 * 1024 * 1024;
            using var stream = file.OpenReadStream(maxAllowedSize: maxFileSize);
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            content.Add(fileContent, "file", file.Name);

            var response = await AuthClient.PostAsync($"api/storage/upload?path={Uri.EscapeDataString(targetPath)}", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file to path: {ex.Message}");
            return false;
        }
    }

    private class DownloadUrlResponse { public string Url { get; set; } = ""; }

    public async Task<List<ProductSearchIndexDto>> GetSearchIndexAsync(string? currency = null, string? language = null)
    {
        if (string.IsNullOrEmpty(language)) language = _localeService.CurrentLocale?.Language;
        var cacheKey = $"search_index_{currency}_{language}";
        var cached = GetFromCache<List<ProductSearchIndexDto>>(cacheKey);
        if (cached != null) return cached;

        try
        {
            var url = "api/products/search-index";
            // Append params
            var query = new List<string>();
            if (!string.IsNullOrEmpty(currency)) query.Add($"currency={currency}");
            if (!string.IsNullOrEmpty(language)) query.Add($"language={language}");
            
            if (query.Count > 0) url += "?" + string.Join("&", query);

            var result = await PublicClient.GetFromJsonAsync<List<ProductSearchIndexDto>>(url) 
                ?? new List<ProductSearchIndexDto>();

            // Cache for 30 min in client RAM to avoid re-fetching often
            SetCache(cacheKey, result, TimeSpan.FromMinutes(30));
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching search index: {ex.Message}");
            return new List<ProductSearchIndexDto>();
        }
    }
}

// Helper class for tracking upload progress
public class ProgressStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _totalBytes;
    private readonly Action<int> _onProgress;
    private long _bytesRead;

    public ProgressStream(Stream baseStream, long totalBytes, Action<int> onProgress)
    {
        _baseStream = baseStream;
        _totalBytes = totalBytes;
        _onProgress = onProgress;
        _bytesRead = 0;
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
    public override void SetLength(long value) => _baseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _baseStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;
        
        if (_totalBytes > 0)
        {
            var progress = (int)((_bytesRead * 100) / _totalBytes);
            _onProgress?.Invoke(Math.Min(progress, 100));
        }
        
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytesRead;
        
        if (_totalBytes > 0)
        {
            var progress = (int)((_bytesRead * 100) / _totalBytes);
            _onProgress?.Invoke(Math.Min(progress, 100));
        }
        
        return bytesRead;
    }
}




