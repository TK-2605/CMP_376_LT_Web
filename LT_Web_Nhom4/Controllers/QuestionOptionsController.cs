using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize(Roles = "Teacher,Admin")]
    public class QuestionOptionsController : CrudController<QuestionOption>
    {
        public QuestionOptionsController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
