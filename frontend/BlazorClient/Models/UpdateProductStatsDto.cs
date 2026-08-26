using System.Text.Json.Serialization;

namespace BlazorClient.Models
{
    public class UpdateProductStatsDto
    {
        [JsonPropertyName("reviewCount")]
        public int ReviewCount { get; set; }

        [JsonPropertyName("averageRating")]
        public double AverageRating { get; set; }
    }
}
