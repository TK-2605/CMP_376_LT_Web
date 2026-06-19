using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ClassMembersController : CrudController<ClassMember>
    {
        public ClassMembersController(ApplicationDbContext context) : base(context)
        {
        }

        protected override IQueryable<ClassMember> ApplyReadScope(IQueryable<ClassMember> query)
        {
            return IsAdmin
                ? query
                : query.Where(member => member.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(ClassMember entity)
        {
            return IsAdmin || await OwnsClassAsync(entity.ClassId);
        }

        protected override Task<bool> CanCreateAsync(ClassMember entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanUpdateAsync(ClassMember entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(ClassMember entity)
        {
            return CanReadAsync(entity);
        }

        private async Task<bool> OwnsClassAsync(int classId)
        {
            return await Context.Classes.AnyAsync(classRoom =>
                classRoom.Id == classId && classRoom.TeacherId == CurrentUserId);
        }
    }
}
