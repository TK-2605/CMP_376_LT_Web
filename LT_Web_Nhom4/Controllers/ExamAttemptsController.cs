using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamAttemptsController : CrudController<ExamAttempt>
    {
        public ExamAttemptsController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
