namespace LT_Web_Nhom4.Models
{
    public class AttemptAnswerSelection
    {
        public int AttemptAnswerId { get; set; }

        public int QuestionOptionId { get; set; }

        public AttemptAnswer AttemptAnswer { get; set; } = null!;

        public QuestionOption QuestionOption { get; set; } = null!;
    }
}
