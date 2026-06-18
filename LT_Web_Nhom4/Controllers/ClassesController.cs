using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class ClassesController : CrudController<ClassEntity>
    {
        public ClassesController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
