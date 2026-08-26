using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IProductService
{
    Task<List<Tag>> GetTagsAsync(bool forceRefresh = false);
    Task<(Tag? Tag, string? Error)> CreateTagAsync(CreateTagDto dto);
    Task<(bool Success, string? Error)> UpdateTagAsync(string id, UpdateTagDto dto);
    Task<bool> DeleteTagAsync(string id);
    Task<ProductListResponse> GetProductsAsync(int page = 1, int pageSize = 10, string? categoryId = null, string? orderBy = null, string? tag = null, string? lastDocId = null, string? type = null, bool? onlyOutOfStock = null, string? currency = null, string? search = null, string? language = null, decimal? minPrice = null, decimal? maxPrice = null, bool includeUnpublished = false);
    Task<List<Product>> GetBestSellingProductsAsync(int count = 8, string? currency = null);
    Task<ProductListResponse> GetFeaturedProductsAsync(string? currency = null);
    Task<Product?> GetProductAsync(string id, string? currency = null);
    Task<Product?> GetAdminProductAsync(string id);
    Task<Product?> CreateProductAsync(CreateProductDto dto);
    Task<bool> UpdateProductAsync(string id, CreateProductDto dto);
    Task<bool> DeleteProductAsync(string id);
    Task<string?> UploadProductImageAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, Action<int>? onProgress = null);
    Task<bool> DeleteProductImageAsync(string imageUrl);
    Task<List<Category>> GetCategoriesAsync(bool forceRefresh = false);
    Task<Category?> CreateCategoryAsync(CreateCategoryDto dto);
    Task<bool> UpdateCategoryAsync(string id, UpdateCategoryDto dto);
    Task<bool> DeleteCategoryAsync(string id);
    
    // Digital file methods
    Task<DigitalFileUploadResponse?> UploadDigitalFileAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, Action<int>? onProgress = null);
    Task<(DigitalFileUploadResponse? Response, string? Error)> UploadDigitalFileFromBytesAsync(byte[] fileBytes, string fileName, Action<int>? onProgress = null);
    Task<bool> DeleteDigitalFileAsync(string path);
    Task<bool> RemoveProductDigitalFileAsync(string productId);
    Task<DownloadLinkResponse?> GetDownloadLinkAsync(string productId);
    Task<DownloadLinkResponse?> GetAdminDownloadLinkAsync(string productId);
    Task<DigitalDownloadListResponse> GetMyDownloadsAsync(int page = 1, int pageSize = 10, string? lastDocId = null);
    Task<FileVersionDetails?> InspectFileVersionsAsync(string path);
    Task<B2BrowserResponse?> BrowseFilesAsync(string path = "", int pageSize = 10, string? continuationToken = null, string? search = null);
    Task<string?> GetB2DownloadUrlAsync(string path);
    Task<bool> DeleteB2FileAsync(string path);
    Task<bool> UploadFileToPathAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, string targetPath = "");
    
    /// <summary>
    /// Busca índice de produtos leve para busca local.
    /// </summary>
    Task<List<ProductSearchIndexDto>> GetSearchIndexAsync(string? currency = null, string? language = null);
    Task<bool> UpdateProductStatsAsync(string id, BlazorClient.Models.UpdateProductStatsDto dto);
}
