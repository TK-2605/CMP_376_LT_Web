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

        protected override async Task OnCreatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            if (entity.SubjectId <= 0)
            {
                entity.SubjectId = await EnsureDefaultSubjectAsync();
            }
        }

        protected override async Task OnUpdatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            if (entity.SubjectId <= 0)
            {
                entity.SubjectId = await EnsureDefaultSubjectAsync();
            }
        }

        private async Task<int> EnsureDefaultSubjectAsync()
        {
            var subject = await Context.Subjects.FirstOrDefaultAsync(item => item.Code == "GENERAL");
            if (subject is not null)
            {
                return subject.Id;
            }

            subject = new LT_Web_Nhom4.Models.Subject
            {
                Code = "GENERAL",
                Name = "Mon hoc chung",
                Description = "Mon hoc mac dinh de tao lop/phong thi nhanh."
            };

            Context.Subjects.Add(subject);
            await Context.SaveChangesAsync();
            return subject.Id;
        }
    }
}
