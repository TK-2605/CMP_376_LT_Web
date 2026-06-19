using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class AttemptAnswersController : CrudController<AttemptAnswer>
    {
        public AttemptAnswersController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<AttemptAnswer> ApplyReadScope(IQueryable<AttemptAnswer> query)
        {
            return IsAdmin
                ? query
                : query.Where(answer =>
                    answer.ExamAttempt.UserId == CurrentUserId
                    || answer.ExamAttempt.Exam.CreatedById == CurrentUserId
                    || answer.ExamAttempt.Exam.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(AttemptAnswer entity)
        {
            return IsAdmin || await OwnsAttemptAsync(entity.ExamAttemptId) || await OwnsExamByAttemptAsync(entity.ExamAttemptId);
        }

        protected override async Task<bool> CanCreateAsync(AttemptAnswer entity)
        {
            return IsAdmin || await CanStudentChangeAnswerAsync(entity.ExamAttemptId);
        }

        protected override Task<bool> CanUpdateAsync(AttemptAnswer entity)
        {
            return CanCreateAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(AttemptAnswer entity)
        {
            return CanCreateAsync(entity);
        }

        protected override Task OnCreatingAsync(AttemptAnswer entity)
        {
            entity.LastSavedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        protected override Task OnUpdatingAsync(AttemptAnswer entity)
        {
            entity.LastSavedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private async Task<bool> OwnsAttemptAsync(int attemptId)
        {
            return await Context.ExamAttempts.AnyAsync(attempt =>
                attempt.Id == attemptId && attempt.UserId == CurrentUserId);
        }

        private async Task<bool> OwnsExamByAttemptAsync(int attemptId)
        {
            return await Context.ExamAttempts.AnyAsync(attempt =>
                attempt.Id == attemptId
                && (attempt.Exam.CreatedById == CurrentUserId || attempt.Exam.Class.TeacherId == CurrentUserId));
        }

        private async Task<bool> CanStudentChangeAnswerAsync(int attemptId)
        {
            return await Context.ExamAttempts.AnyAsync(attempt =>
                attempt.Id == attemptId
                && attempt.UserId == CurrentUserId
                && attempt.SubmittedAt == null
                && attempt.Status == ExamAttemptStatus.InProgress);
        }
    }
}
