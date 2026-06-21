using LT_Web_Nhom4.Areas.Admin.Models;
using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ExamAttemptsController : CrudController<ExamAttempt>
    {
        private readonly ApplicationDbContext _context;

        public ExamAttemptsController(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public override async Task<IActionResult> Index()
        {
            var attempts = await _context.ExamAttempts
                .AsNoTracking()
                .Include(attempt => attempt.Exam)
                .Include(attempt => attempt.User)
                .Include(attempt => attempt.AntiCheatEvents)
                .OrderByDescending(attempt => attempt.StartedAt)
                .Take(200)
                .Select(attempt => new AdminExamAttemptViewModel
                {
                    Id = attempt.Id,
                    ExamTitle = attempt.Exam.Title,
                    StudentName = attempt.User.FullName != string.Empty
                        ? attempt.User.FullName
                        : attempt.User.UserName ?? attempt.User.Email ?? attempt.UserId,
                    StudentEmail = attempt.User.Email ?? string.Empty,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    Score = attempt.Score,
                    Status = attempt.Status,
                    AntiCheatEventCount = attempt.AntiCheatEvents.Count
                })
                .ToListAsync();

            return View(attempts);
        }
    }
}
