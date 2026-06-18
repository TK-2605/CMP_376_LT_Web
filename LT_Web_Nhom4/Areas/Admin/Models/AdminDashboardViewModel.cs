namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        public int TotalSubjects { get; set; }

        public int TotalClasses { get; set; }

        public int TotalQuestions { get; set; }

        public int TotalExams { get; set; }

        public int TotalExamAttempts { get; set; }

        public int TotalAntiCheatEvents { get; set; }

        public IList<AdminUserSummaryViewModel> RecentUsers { get; set; } = new List<AdminUserSummaryViewModel>();

        public IList<AdminExamSummaryViewModel> RecentExams { get; set; } = new List<AdminExamSummaryViewModel>();
    }
}
