using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamQuestionsController : CrudController<ExamQuestion>
    {
        public ExamQuestionsController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<ExamQuestion> ApplyReadScope(IQueryable<ExamQuestion> query)
        {
            return IsAdmin
                ? query
                : query.Where(examQuestion =>
                    examQuestion.Exam.CreatedById == CurrentUserId
                    || examQuestion.Exam.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(ExamQuestion entity)
        {
            return IsAdmin || await OwnsExamAsync(entity.ExamId);
        }

        protected override Task<bool> CanCreateAsync(ExamQuestion entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanUpdateAsync(ExamQuestion entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(ExamQuestion entity)
        {
            return CanReadAsync(entity);
        }

        private async Task<bool> OwnsExamAsync(int examId)
        {
            return await Context.Exams.AnyAsync(exam =>
                exam.Id == examId
                && (exam.CreatedById == CurrentUserId || exam.Class.TeacherId == CurrentUserId));
        }
    }
}
