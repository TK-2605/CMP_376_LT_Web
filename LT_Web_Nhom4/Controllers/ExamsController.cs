using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamsController : CrudController<Exam>
    {
        private readonly IExamRepository _examRepository;
        private readonly IExamQuestionRepository _examQuestionRepository;
        private readonly IExamAttemptRepository _examAttemptRepository;
        private readonly IAttemptAnswerRepository _attemptAnswerRepository;

        public ExamsController(
            IExamRepository examRepository,
            IExamQuestionRepository examQuestionRepository,
            IExamAttemptRepository examAttemptRepository,
            IAttemptAnswerRepository attemptAnswerRepository,
            ApplicationDbContext context) : base(context)
        {
            _examRepository = examRepository;
            _examQuestionRepository = examQuestionRepository;
            _examAttemptRepository = examAttemptRepository;
            _attemptAnswerRepository = attemptAnswerRepository;
        }

        public override Task<IActionResult> Index()
        {
            var model = new ExamListViewModel
            {
                Exams =
                {
                    new ExamCardViewModel
                    {
                        Id = 1,
                        Title = "ASP.NET Core MVC Basics",
                        SubjectName = "Lap trinh Web",
                        ClassName = "D21_TH01",
                        StartAt = DateTime.Now.AddHours(1),
                        EndAt = DateTime.Now.AddHours(3),
                        DurationMinutes = 60,
                        QuestionCount = 20
                    },
                    new ExamCardViewModel
                    {
                        Id = 2,
                        Title = "Entity Framework Core",
                        SubjectName = "Cong nghe .NET",
                        ClassName = "D21_TH02",
                        StartAt = DateTime.Now.AddDays(1),
                        EndAt = DateTime.Now.AddDays(1).AddHours(2),
                        DurationMinutes = 45,
                        QuestionCount = 15
                    }
                }
            };

            return Task.FromResult<IActionResult>(View(model));
        }

        public IActionResult Start(int id)
        {
            var model = BuildSampleExam(id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit(ExamSubmitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var exam = BuildSampleExam(model.ExamId);
                for (var i = 0; i < exam.Questions.Count; i++)
                {
                    var submittedAnswer = model.Answers.FirstOrDefault(answer => answer.QuestionId == exam.Questions[i].QuestionId);
                    if (submittedAnswer is not null)
                    {
                        ViewData[$"SelectedOption_{exam.Questions[i].QuestionId}"] = submittedAnswer.SelectedOptionId;
                    }
                }

                ModelState.AddModelError(string.Empty, "Bai lam chua hop le. Vui long kiem tra lai dap an truoc khi nop.");
                return View("Start", exam);
            }

            TempData["ExamMessage"] = "Bai lam da duoc gui thanh cong.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Autosave([FromForm] ExamSubmitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { ok = false, message = "Du lieu autosave khong hop le." });
            }

            return Ok(new { ok = true, savedAt = DateTime.Now.ToString("HH:mm:ss") });
        }

        private static ExamStartViewModel BuildSampleExam(int id)
        {
            return new ExamStartViewModel
            {
                ExamId = id,
                AttemptId = 1000 + id,
                Title = "ASP.NET Core MVC Basics",
                SubjectName = "Lap trinh Web",
                DurationMinutes = 45,
                StartedAt = DateTime.Now,
                Questions =
                {
                    new ExamQuestionViewModel
                    {
                        QuestionId = 101,
                        Content = "Trong ASP.NET Core MVC, thanh phan nao nhan request va tra ve response?",
                        Score = 1,
                        Options =
                        {
                            new ExamOptionViewModel { OptionId = 1001, Content = "Controller" },
                            new ExamOptionViewModel { OptionId = 1002, Content = "Migration" },
                            new ExamOptionViewModel { OptionId = 1003, Content = "DbSet" },
                            new ExamOptionViewModel { OptionId = 1004, Content = "ViewModel" }
                        }
                    },
                    new ExamQuestionViewModel
                    {
                        QuestionId = 102,
                        Content = "Tag Helper nao giup bind input voi property trong model?",
                        Score = 1,
                        Options =
                        {
                            new ExamOptionViewModel { OptionId = 2001, Content = "asp-for" },
                            new ExamOptionViewModel { OptionId = 2002, Content = "asp-route" },
                            new ExamOptionViewModel { OptionId = 2003, Content = "asp-area" },
                            new ExamOptionViewModel { OptionId = 2004, Content = "asp-page" }
                        }
                    },
                    new ExamQuestionViewModel
                    {
                        QuestionId = 103,
                        Content = "Thuoc tinh nao nen dung de hien thi loi validation cho mot field?",
                        Score = 1,
                        Options =
                        {
                            new ExamOptionViewModel { OptionId = 3001, Content = "asp-validation-for" },
                            new ExamOptionViewModel { OptionId = 3002, Content = "asp-action" },
                            new ExamOptionViewModel { OptionId = 3003, Content = "asp-controller" },
                            new ExamOptionViewModel { OptionId = 3004, Content = "asp-fragment" }
                        }
                    }
                }
            };
        }
    }
}
