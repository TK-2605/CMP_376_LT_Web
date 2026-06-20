using System.Diagnostics;
using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(new WebHomeViewModel());
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isAdmin = User.IsInRole("Admin");

            var ownedClasses = await _context.Classes.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Exams).Include(item => item.Members)
                .Where(item => isAdmin || item.TeacherId == userId)
                .OrderByDescending(item => item.CreatedAt).Take(4).ToListAsync();

            var joinedClasses = await _context.Classes.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Exams).Include(item => item.Members)
                .Where(item => item.Members.Any(member => member.UserId == userId && member.Status == ClassMemberStatus.Active))
                .OrderByDescending(item => item.CreatedAt).Take(4).ToListAsync();

            var upcoming = await _context.Exams.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Class).Include(item => item.ExamQuestions)
                .Where(item => item.Status == ExamStatus.Published
                    && item.EndAt >= DateTime.Now
                    && (isAdmin || item.CreatedById == userId || item.Class.TeacherId == userId
                        || item.Class.Members.Any(member => member.UserId == userId && member.Status == ClassMemberStatus.Active)))
                .OrderBy(item => item.StartAt).Take(6).ToListAsync();

            return View(new WebHomeViewModel
            {
                IsAuthenticated = true,
                OwnedClasses = ownedClasses.Select(ToClassCard).ToList(),
                ParticipatingClasses = joinedClasses.Where(item => item.TeacherId != userId).Select(ToClassCard).ToList(),
                UpcomingExams = upcoming.Select(item => new ExamCardViewModel
                {
                    Id = item.Id,
                    Code = item.Code,
                    Title = item.Title,
                    SubjectName = item.Subject.Name,
                    ClassName = item.Class.Name,
                    StartAt = item.StartAt,
                    EndAt = item.EndAt,
                    DurationMinutes = item.DurationMinutes,
                    QuestionCount = item.ExamQuestions.Count,
                    Status = item.Status,
                    IsOwnedByCurrentUser = isAdmin || item.CreatedById == userId || item.Class.TeacherId == userId,
                    IsParticipant = !isAdmin && item.CreatedById != userId && item.Class.TeacherId != userId
                }).ToList()
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private ClassCardViewModel ToClassCard(Models.Class item)
        {
            return new ClassCardViewModel
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                SubjectName = item.Subject.Name,
                Description = item.Description,
                CoverImageUrl = item.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = item.Id }),
                Semester = item.Semester,
                AcademicYear = item.AcademicYear,
                ExamCount = item.Exams.Count,
                MemberCount = item.Members.Count(member => member.Status == ClassMemberStatus.Active)
            };
        }
    }
}
