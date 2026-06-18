namespace LT_Web_Nhom4.Models
{
    public class ExamQuestion
    {
        public int Id { get; set; }

        public int ExamId { get; set; }

        public int QuestionId { get; set; }

        public decimal Score { get; set; }

        public int DisplayOrder { get; set; }

        public Exam Exam { get; set; } = null!;

        public Question Question { get; set; } = null!;
    }
}
