using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamAttemptsController : CrudController<ExamAttempt>
    {
        public ExamAttemptsController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<ExamAttempt> ApplyReadScope(IQueryable<ExamAttempt> query)
        {
            return IsAdmin
                ? query
                : query.Where(attempt =>
                    attempt.UserId == CurrentUserId
                    || attempt.Exam.CreatedById == CurrentUserId
                    || attempt.Exam.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(ExamAttempt entity)
        {
            return IsAdmin || entity.UserId == CurrentUserId || await OwnsExamAsync(entity.ExamId);
        }

        protected override async Task<bool> CanCreateAsync(ExamAttempt entity)
        {
            return IsAdmin || entity.UserId == CurrentUserId || await OwnsExamAsync(entity.ExamId);
        }

        protected override async Task<bool> CanUpdateAsync(ExamAttempt entity)
        {
            return IsAdmin || await OwnsExamAsync(entity.ExamId);
        }

        protected override Task<bool> CanDeleteAsync(ExamAttempt entity)
        {
            return CanUpdateAsync(entity);
        }

        protected override Task OnCreatingAsync(ExamAttempt entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.UserId = CurrentUserId;
            }

            return Task.CompletedTask;
        }

        private async Task<bool> OwnsExamAsync(int examId)
        {
            return await Context.Exams.AnyAsync(exam =>
                exam.Id == examId
                && (exam.CreatedById == CurrentUserId || exam.Class.TeacherId == CurrentUserId));
        }
    }
}
