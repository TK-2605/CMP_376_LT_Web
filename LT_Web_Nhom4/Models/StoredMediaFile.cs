namespace LT_Web_Nhom4.Models
{
    public class StoredMediaFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Category { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long Length { get; set; }

        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
