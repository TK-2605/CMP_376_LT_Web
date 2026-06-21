namespace LT_Web_Nhom4.Models
{
    public class ClassMedia
    {
        public int Id { get; set; }

        public int ClassId { get; set; }

        public MediaAssetType MediaType { get; set; }

        public string Path { get; set; } = string.Empty;

        public string? OriginalFileName { get; set; }

        public int DisplayOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Class Class { get; set; } = null!;
    }
}
