using System;
using System.ComponentModel.DataAnnotations;

namespace BlazorClient.Models
{
    public class TranslationMigrationResult
    {
        public int MigratedCount { get; set; }
        public int FailedCount { get; set; }
        public List<string> Errors { get; set; } = new();

        // Aliases for compatibility with different backend responses
        public int ProductsTranslated { get => MigratedCount; set => MigratedCount = value; }
        public int Migrated { get => MigratedCount; set => MigratedCount = value; }
    }

    public class ProductReview
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string ProductId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int Rating { get; set; } // 1-5

        [Required(ErrorMessage = "O comentário é obrigatório")]
        [StringLength(1000, ErrorMessage = "O comentário não pode exceder 1000 caracteres")]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsHidden { get; set; }
    }

    public class ReviewListResponse
    {
        public List<ProductReview> Items { get; set; } = new();
        public PaginationData Pagination { get; set; } = new();
    }
}
