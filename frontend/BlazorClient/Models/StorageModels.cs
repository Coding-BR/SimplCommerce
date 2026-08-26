using System.Collections.Generic;

namespace BlazorClient.Models;

public class FileVersionDetails
{
    public string Path { get; set; } = string.Empty;
    public int VersionCount { get; set; }
    public int DeleteMarkerCount { get; set; }
    public long TotalSize { get; set; }
    public List<string> Details { get; set; } = new();
}

public class B2FileItem
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
}

public class B2BrowserResponse
{
    public string CurrentPath { get; set; } = string.Empty;
    public List<B2FileItem> Items { get; set; } = new();
    
    // Pagination
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public string? ContinuationToken { get; set; }
    public bool HasMore { get; set; }
}
