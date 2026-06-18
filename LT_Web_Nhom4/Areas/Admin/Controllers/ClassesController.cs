using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ClassesController : CrudController<ClassEntity>
    {
        public ClassesController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
