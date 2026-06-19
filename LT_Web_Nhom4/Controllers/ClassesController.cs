using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ClassesController : CrudController<ClassEntity>
    {
        public ClassesController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<ClassEntity> ApplyReadScope(IQueryable<ClassEntity> query)
        {
            return IsAdmin ? query : query.Where(classRoom => classRoom.TeacherId == CurrentUserId);
        }

        protected override Task<bool> CanReadAsync(ClassEntity entity)
        {
            return Task.FromResult(IsAdmin || entity.TeacherId == CurrentUserId);
        }

        protected override Task<bool> CanCreateAsync(ClassEntity entity)
        {
            return Task.FromResult(User.Identity?.IsAuthenticated == true);
        }

        protected override Task<bool> CanUpdateAsync(ClassEntity entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(ClassEntity entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task OnCreatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            return Task.CompletedTask;
        }

        protected override Task OnUpdatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            return Task.CompletedTask;
        }
    }
}
