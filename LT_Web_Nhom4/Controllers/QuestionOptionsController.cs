using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class QuestionOptionsController : CrudController<QuestionOption>
    {
        public QuestionOptionsController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<QuestionOption> ApplyReadScope(IQueryable<QuestionOption> query)
        {
            return IsAdmin ? query : query.Where(option => option.Question.CreatedById == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(QuestionOption entity)
        {
            return IsAdmin || await OwnsQuestionAsync(entity.QuestionId);
        }

        protected override Task<bool> CanCreateAsync(QuestionOption entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanUpdateAsync(QuestionOption entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(QuestionOption entity)
        {
            return CanReadAsync(entity);
        }

        private async Task<bool> OwnsQuestionAsync(int questionId)
        {
            return await Context.Questions.AnyAsync(question =>
                question.Id == questionId && question.CreatedById == CurrentUserId);
        }
    }
}
