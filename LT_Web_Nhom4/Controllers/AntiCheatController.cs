using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class AntiCheatController : CrudController<AntiCheatEvent>
    {
        public AntiCheatController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<AntiCheatEvent> ApplyReadScope(IQueryable<AntiCheatEvent> query)
        {
            return IsAdmin
                ? query
                : query.Where(antiCheatEvent =>
                    antiCheatEvent.ExamAttempt.Exam.CreatedById == CurrentUserId
                    || antiCheatEvent.ExamAttempt.Exam.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(AntiCheatEvent entity)
        {
            return IsAdmin || await OwnsExamByAttemptAsync(entity.ExamAttemptId);
        }

        protected override Task<bool> CanCreateAsync(AntiCheatEvent entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanUpdateAsync(AntiCheatEvent entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(AntiCheatEvent entity)
        {
            return CanReadAsync(entity);
        }

        private async Task<bool> OwnsExamByAttemptAsync(int attemptId)
        {
            return await Context.ExamAttempts.AnyAsync(attempt =>
                attempt.Id == attemptId
                && (attempt.Exam.CreatedById == CurrentUserId || attempt.Exam.Class.TeacherId == CurrentUserId));
        }
    }
}
