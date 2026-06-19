using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class QuestionsController : CrudController<Question>
    {
        public QuestionsController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<Question> ApplyReadScope(IQueryable<Question> query)
        {
            return IsAdmin ? query : query.Where(question => question.CreatedById == CurrentUserId);
        }

        protected override Task<bool> CanReadAsync(Question entity)
        {
            return Task.FromResult(IsAdmin || entity.CreatedById == CurrentUserId);
        }

        protected override Task<bool> CanCreateAsync(Question entity)
        {
            return Task.FromResult(User.Identity?.IsAuthenticated == true);
        }

        protected override Task<bool> CanUpdateAsync(Question entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(Question entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task OnCreatingAsync(Question entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.CreatedById = CurrentUserId;
            }

            return Task.CompletedTask;
        }

        protected override Task OnUpdatingAsync(Question entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.CreatedById = CurrentUserId;
            }

            return Task.CompletedTask;
        }
    }
}
