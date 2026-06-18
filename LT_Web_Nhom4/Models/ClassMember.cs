namespace LT_Web_Nhom4.Models
{
    public class ClassMember
    {
        public int ClassId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public ClassMemberStatus Status { get; set; } = ClassMemberStatus.Active;

        public Class Class { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;
    }
}
