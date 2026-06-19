using System.Diagnostics;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LT_Web_Nhom4.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var model = new WebHomeViewModel
            {
                FeaturedSubjects =
                {
                    new FeaturedSubjectViewModel
                    {
                        Code = "WEB",
                        Name = "Lap trinh Web",
                        Description = "On tap ly thuyet, bai tap va cac tinh huong xay dung website.",
                        ImageUrl = "/img/student/course-01.jpg",
                        ExamCount = 6
                    },
                    new FeaturedSubjectViewModel
                    {
                        Code = "NET",
                        Name = "Cong nghe .NET",
                        Description = "Cung co kien thuc nen tang ve .NET, du lieu va ung dung web.",
                        ImageUrl = "/img/student/course-02.jpg",
                        ExamCount = 4
                    }
                },
                UpcomingExams =
                {
                    new UpcomingExamViewModel
                    {
                        Id = 1,
                        Title = "Kiem tra Lap trinh Web co ban",
                        SubjectName = "Lap trinh Web",
                        StartAt = DateTime.Now.AddHours(1),
                        DurationMinutes = 60
                    },
                    new UpcomingExamViewModel
                    {
                        Id = 2,
                        Title = "On tap Cong nghe .NET",
                        SubjectName = "Cong nghe .NET",
                        StartAt = DateTime.Now.AddDays(1),
                        DurationMinutes = 45
                    }
                }
            };

            return View(model);
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
    }
}
