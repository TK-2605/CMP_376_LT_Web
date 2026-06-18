using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuestionsController : CrudController<Question>
    {
        public QuestionsController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
