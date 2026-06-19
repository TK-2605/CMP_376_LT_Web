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
                Exams = BuildSampleExamCards()
            };

            return Task.FromResult<IActionResult>(View(model));
        }

        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> Details(string id)
        {
            return base.Details(id);
        }

        [Authorize(Roles = "Teacher,Admin")]
        public override IActionResult Create()
        {
            return base.Create();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> Create(IFormCollection form)
        {
            return base.Create(form);
        }

        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> Edit(string id)
        {
            return base.Edit(id);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> Edit(string id, IFormCollection form)
        {
            return base.Edit(id, form);
        }

        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> Delete(string id)
        {
            return base.Delete(id);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Teacher,Admin")]
        public override Task<IActionResult> DeleteConfirmed(string id)
        {
            return base.DeleteConfirmed(id);
        }

        public IActionResult Room(int id)
        {
            var room = BuildSampleRoom(id);
            return room.IsOpen ? View("Confirm", room) : View("Waiting", room);
        }

        [HttpGet]
        public IActionResult Start(int id)
        {
            return RedirectToAction(nameof(Room), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Start(int id, bool acceptRules)
        {
            var room = BuildSampleRoom(id);
            if (!room.IsOpen)
            {
                return View("Waiting", room);
            }

            if (!acceptRules)
            {
                ModelState.AddModelError(string.Empty, "Vui long xac nhan da doc quy dinh truoc khi bat dau.");
                return View("Confirm", room);
            }

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
                return BadRequest(new { ok = false, message = "Du lieu tu dong luu khong hop le." });
            }

            return Ok(new { ok = true, savedAt = DateTime.Now.ToString("HH:mm:ss") });
        }

        private static IList<ExamCardViewModel> BuildSampleExamCards()
        {
            var now = DateTime.Now;
            return new List<ExamCardViewModel>
            {
                new()
                {
                    Id = 1,
                    Title = "Kiem tra Lap trinh Web co ban",
                    SubjectName = "Lap trinh Web",
                    ClassName = "D21_TH01",
                    StartAt = now.AddHours(1),
                    EndAt = now.AddHours(3),
                    DurationMinutes = 60,
                    QuestionCount = 20
                },
                new()
                {
                    Id = 2,
                    Title = "On tap Cong nghe .NET",
                    SubjectName = "Cong nghe .NET",
                    ClassName = "D21_TH02",
                    StartAt = now.AddHours(1),
                    EndAt = now.AddHours(2),
                    DurationMinutes = 45,
                    QuestionCount = 15
                },
                new()
                {
                    Id = 3,
                    Title = "Bai thi mau dang mo",
                    SubjectName = "Lap trinh Web",
                    ClassName = "D21_TH03",
                    StartAt = now.AddMinutes(-10),
                    EndAt = now.AddMinutes(50),
                    DurationMinutes = 60,
                    QuestionCount = 10
                }
            };
        }

        private static ExamRoomViewModel BuildSampleRoom(int id)
        {
            var card = BuildSampleExamCards().FirstOrDefault(exam => exam.Id == id)
                ?? BuildSampleExamCards().First();

            return new ExamRoomViewModel
            {
                Id = card.Id,
                Title = card.Title,
                SubjectName = card.SubjectName,
                ClassName = card.ClassName,
                StartAt = card.StartAt,
                EndAt = card.EndAt,
                DurationMinutes = card.DurationMinutes,
                QuestionCount = card.QuestionCount,
                Now = DateTime.Now
            };
        }

        private static ExamStartViewModel BuildSampleExam(int id)
        {
            var room = BuildSampleRoom(id);
            return new ExamStartViewModel
            {
                ExamId = id,
                AttemptId = 1000 + id,
                Title = room.Title,
                SubjectName = room.SubjectName,
                DurationMinutes = room.DurationMinutes,
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
