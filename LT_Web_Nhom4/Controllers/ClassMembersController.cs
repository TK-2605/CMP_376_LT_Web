using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class ClassMembersController : CrudController<ClassMember>
    {
        public ClassMembersController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
