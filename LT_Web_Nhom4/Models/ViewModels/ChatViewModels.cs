namespace LT_Web_Nhom4.Models.ViewModels
{
    public class ChatRoomViewModel
    {
        public string RoomType { get; set; } = string.Empty;

        public int RoomId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string BackUrl { get; set; } = "/";
    }
}
