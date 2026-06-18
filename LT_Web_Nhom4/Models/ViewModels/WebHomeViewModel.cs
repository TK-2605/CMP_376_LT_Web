namespace LT_Web_Nhom4.Models.ViewModels
{
    public class WebHomeViewModel
    {
        public IList<FeaturedSubjectViewModel> FeaturedSubjects { get; set; } = new List<FeaturedSubjectViewModel>();

        public IList<UpcomingExamViewModel> UpcomingExams { get; set; } = new List<UpcomingExamViewModel>();
    }

    public class FeaturedSubjectViewModel
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public int ExamCount { get; set; }
    }

    public class UpcomingExamViewModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string SubjectName { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public int DurationMinutes { get; set; }
    }
}
