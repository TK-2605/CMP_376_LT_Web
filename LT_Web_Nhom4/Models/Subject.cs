namespace LT_Web_Nhom4.Models
{
    public class Subject
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Class> Classes { get; set; } = new List<Class>();

        public ICollection<Question> Questions { get; set; } = new List<Question>();

        public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
