using LT_Web_Nhom4.Areas.Admin.Models;
using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                ActiveUsers = await _context.Users.CountAsync(user => user.IsActive),
                TotalSubjects = await _context.Subjects.CountAsync(),
                TotalClasses = await _context.Classes.CountAsync(),
                TotalQuestions = await _context.Questions.CountAsync(),
                TotalExams = await _context.Exams.CountAsync(),
                TotalExamAttempts = await _context.ExamAttempts.CountAsync(),
                TotalAntiCheatEvents = await _context.AntiCheatEvents.CountAsync(),
                RecentUsers = await _context.Users
                    .OrderByDescending(user => user.CreatedAt)
                    .Take(5)
                    .Select(user => new AdminUserSummaryViewModel
                    {
                        Id = user.Id,
                        Email = user.Email ?? string.Empty,
                        FullName = user.FullName,
                        StudentCode = user.StudentCode,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    })
                    .ToListAsync(),
                RecentExams = await _context.Exams
                    .OrderByDescending(exam => exam.CreatedAt)
                    .Take(5)
                    .Select(exam => new AdminExamSummaryViewModel
                    {
                        Id = exam.Id,
                        Title = exam.Title,
                        SubjectName = exam.Subject.Name,
                        ClassName = exam.Class.Name,
                        CreatedByName = exam.CreatedBy.FullName,
                        Status = exam.Status,
                        StartAt = exam.StartAt,
                        EndAt = exam.EndAt,
                        QuestionCount = exam.ExamQuestions.Count,
                        AttemptCount = exam.Attempts.Count
                    })
                    .ToListAsync()
            };

            return View(model);
        }
    }
}
