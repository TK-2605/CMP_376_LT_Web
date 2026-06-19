namespace LT_Web_Nhom4.Models.ViewModels
{
    public class WebHomeViewModel
    {
        public bool IsAuthenticated { get; set; }

        public JoinClassViewModel JoinClass { get; set; } = new();

        public IList<ClassCardViewModel> OwnedClasses { get; set; } = new List<ClassCardViewModel>();

        public IList<ClassCardViewModel> ParticipatingClasses { get; set; } = new List<ClassCardViewModel>();

        public IList<ExamCardViewModel> UpcomingExams { get; set; } = new List<ExamCardViewModel>();
    }
}
