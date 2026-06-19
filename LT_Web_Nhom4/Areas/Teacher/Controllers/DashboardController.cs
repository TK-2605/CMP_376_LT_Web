using System.Security.Claims;
using LT_Web_Nhom4.Areas.Teacher.Models;
using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Teacher.Controllers
{
    [Area("Teacher")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var model = new TeacherDashboardViewModel
            {
                TotalClasses = isAdmin
                    ? await _context.Classes.CountAsync()
                    : await _context.Classes.CountAsync(classRoom => classRoom.TeacherId == userId),
                TotalQuestions = isAdmin
                    ? await _context.Questions.CountAsync()
                    : await _context.Questions.CountAsync(question => question.CreatedById == userId),
                TotalExams = isAdmin
                    ? await _context.Exams.CountAsync()
                    : await _context.Exams.CountAsync(exam => exam.CreatedById == userId),
                TotalWarnings = isAdmin
                    ? await _context.AntiCheatEvents.CountAsync()
                    : await _context.AntiCheatEvents.CountAsync(antiCheatEvent =>
                        antiCheatEvent.ExamAttempt.Exam.CreatedById == userId
                        || antiCheatEvent.ExamAttempt.Exam.Class.TeacherId == userId)
            };

            return View(model);
        }
    }
}
