using Microsoft.AspNetCore.Identity;

namespace LT_Web_Nhom4.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public string? StudentCode { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Class> ClassesTeaching { get; set; } = new List<Class>();

        public ICollection<ClassMember> ClassMembers { get; set; } = new List<ClassMember>();

        public ICollection<Question> QuestionsCreated { get; set; } = new List<Question>();

        public ICollection<Exam> ExamsCreated { get; set; } = new List<Exam>();

        public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();

        public ICollection<AntiCheatEvent> AntiCheatEvents { get; set; } = new List<AntiCheatEvent>();

        public ICollection<EmailOtp> EmailOtps { get; set; } = new List<EmailOtp>();

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}
