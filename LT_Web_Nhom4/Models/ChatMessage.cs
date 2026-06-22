namespace LT_Web_Nhom4.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public string RoomType { get; set; } = string.Empty;

        public int RoomId { get; set; }

        public string SenderId { get; set; } = string.Empty;

        public string SenderName { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
