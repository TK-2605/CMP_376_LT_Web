namespace LT_Web_Nhom4.Models
{
    public class QuestionOption
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string Content { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public int DisplayOrder { get; set; }

        public Question Question { get; set; } = null!;

        public ICollection<AttemptAnswerSelection> AttemptAnswerSelections { get; set; } = new List<AttemptAnswerSelection>();
    }
}
