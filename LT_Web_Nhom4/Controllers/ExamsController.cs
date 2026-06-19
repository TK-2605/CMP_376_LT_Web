using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public override async Task<IActionResult> Index()
        {
            var cards = await BuildExamCardsAsync();
            if (cards.Count == 0)
            {
                cards = BuildSampleExamCards().ToList();
            }

            return View(new ExamListViewModel { Exams = cards });
        }

        protected override IQueryable<Exam> ApplyReadScope(IQueryable<Exam> query)
        {
            return IsAdmin
                ? query
                : query.Where(exam => exam.CreatedById == CurrentUserId || exam.Class.TeacherId == CurrentUserId);
        }

        protected override async Task<bool> CanReadAsync(Exam entity)
        {
            return IsAdmin || entity.CreatedById == CurrentUserId || await OwnsClassAsync(entity.ClassId);
        }

        protected override Task<bool> CanCreateAsync(Exam entity)
        {
            return Task.FromResult(User.Identity?.IsAuthenticated == true);
        }

        protected override async Task<bool> CanUpdateAsync(Exam entity)
        {
            return IsAdmin || entity.CreatedById == CurrentUserId || await OwnsClassAsync(entity.ClassId);
        }

        protected override Task<bool> CanDeleteAsync(Exam entity)
        {
            return CanUpdateAsync(entity);
        }

        protected override Task OnCreatingAsync(Exam entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.CreatedById = CurrentUserId;
            }

            return Task.CompletedTask;
        }

        protected override Task OnUpdatingAsync(Exam entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.CreatedById = CurrentUserId;
            }

            return Task.CompletedTask;
        }

        public override Task<IActionResult> Details(string id)
        {
            return base.Details(id);
        }

        public async Task<IActionResult> Manage(int id)
        {
            var model = await BuildManageModelAsync(id);
            if (model is null)
            {
                var sample = BuildSampleManageModel(id);
                if (sample is null)
                {
                    return NotFound();
                }

                return View(sample);
            }

            if (!await CanManageExamAsync(id))
            {
                return Forbid();
            }

            return View(model);
        }

        public async Task<IActionResult> Room(int id)
        {
            if (await CanManageExamAsync(id))
            {
                return RedirectToAction(nameof(Manage), new { id });
            }

            var room = await BuildRoomModelAsync(id) ?? BuildSampleRoom(id);
            if (!await CanEnterRoomAsync(id))
            {
                return Forbid();
            }

            return room.IsOpen ? View("Confirm", room) : View("Waiting", room);
        }

        [HttpGet]
        public IActionResult Start(int id)
        {
            return RedirectToAction(nameof(Room), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id, bool acceptRules)
        {
            if (!await CanEnterRoomAsync(id) || await CanManageExamAsync(id))
            {
                return Forbid();
            }

            var room = await BuildRoomModelAsync(id) ?? BuildSampleRoom(id);
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
                    QuestionCount = 20,
                    IsOwnedByCurrentUser = true
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
                    QuestionCount = 15,
                    IsParticipant = true
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
                    QuestionCount = 10,
                    IsParticipant = true
                }
            };
        }

        private async Task<List<ExamCardViewModel>> BuildExamCardsAsync()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<ExamCardViewModel>();
            }

            var exams = await Context.Exams
                .AsNoTracking()
                .Include(exam => exam.Subject)
                .Include(exam => exam.Class)
                .Include(exam => exam.ExamQuestions)
                .Where(exam =>
                    IsAdmin
                    || exam.CreatedById == userId
                    || exam.Class.TeacherId == userId
                    || exam.Class.Members.Any(member => member.UserId == userId && member.Status == ClassMemberStatus.Active))
                .OrderByDescending(exam => exam.StartAt)
                .Take(100)
                .ToListAsync();

            return exams.Select(exam =>
            {
                var ownsExam = IsAdmin || exam.CreatedById == userId || exam.Class.TeacherId == userId;
                return new ExamCardViewModel
                {
                    Id = exam.Id,
                    Title = exam.Title,
                    SubjectName = exam.Subject?.Name ?? "Chua co mon",
                    ClassName = exam.Class?.Name ?? "Chua co lop",
                    StartAt = exam.StartAt,
                    EndAt = exam.EndAt,
                    DurationMinutes = exam.DurationMinutes,
                    QuestionCount = exam.ExamQuestions.Count,
                    IsOwnedByCurrentUser = ownsExam,
                    IsParticipant = !ownsExam
                };
            }).ToList();
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

        private async Task<ExamRoomViewModel?> BuildRoomModelAsync(int id)
        {
            var exam = await Context.Exams
                .AsNoTracking()
                .Include(item => item.Subject)
                .Include(item => item.Class)
                .Include(item => item.ExamQuestions)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (exam is null)
            {
                return null;
            }

            return new ExamRoomViewModel
            {
                Id = exam.Id,
                Title = exam.Title,
                SubjectName = exam.Subject?.Name ?? "Chua co mon",
                ClassName = exam.Class?.Name ?? "Chua co lop",
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                DurationMinutes = exam.DurationMinutes,
                QuestionCount = exam.ExamQuestions.Count,
                Now = DateTime.Now
            };
        }

        private async Task<ExamManageViewModel?> BuildManageModelAsync(int id)
        {
            var exam = await Context.Exams
                .AsNoTracking()
                .Include(item => item.Subject)
                .Include(item => item.Class)
                    .ThenInclude(classRoom => classRoom.Members)
                        .ThenInclude(member => member.User)
                .Include(item => item.ExamQuestions)
                    .ThenInclude(examQuestion => examQuestion.Question)
                        .ThenInclude(question => question.Options)
                .Include(item => item.Attempts)
                    .ThenInclude(attempt => attempt.User)
                .Include(item => item.Attempts)
                    .ThenInclude(attempt => attempt.AntiCheatEvents)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (exam is null)
            {
                return null;
            }

            var attempts = exam.Attempts.OrderByDescending(attempt => attempt.StartedAt).ToList();
            var submittedAttempts = attempts.Where(attempt => attempt.SubmittedAt is not null || attempt.Status == ExamAttemptStatus.Submitted).ToList();

            return new ExamManageViewModel
            {
                Id = exam.Id,
                Title = exam.Title,
                SubjectName = exam.Subject?.Name ?? "Chua co mon",
                ClassName = exam.Class?.Name ?? "Chua co lop",
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                DurationMinutes = exam.DurationMinutes,
                QuestionCount = exam.ExamQuestions.Count,
                ParticipantCount = exam.Class?.Members.Count(member => member.Status == ClassMemberStatus.Active) ?? 0,
                AttemptCount = attempts.Count,
                SubmittedCount = submittedAttempts.Count,
                WarningCount = attempts.Sum(attempt => attempt.AntiCheatEvents.Count),
                AverageScore = submittedAttempts.Where(attempt => attempt.Score.HasValue).Select(attempt => attempt.Score!.Value).DefaultIfEmpty().Average(),
                Participants = exam.Class?.Members
                    .Where(member => member.Status == ClassMemberStatus.Active)
                    .Select(member => new ExamParticipantViewModel
                    {
                        UserId = member.UserId,
                        DisplayName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email ?? "Hoc sinh" : member.User.FullName,
                        Email = member.User.Email ?? string.Empty,
                        Status = member.Status.ToString()
                    })
                    .ToList() ?? new List<ExamParticipantViewModel>(),
                Attempts = attempts.Select(attempt => new ExamAttemptSummaryViewModel
                    {
                        AttemptId = attempt.Id,
                        StudentName = string.IsNullOrWhiteSpace(attempt.User.FullName) ? attempt.User.Email ?? "Hoc sinh" : attempt.User.FullName,
                        StartedAt = attempt.StartedAt,
                        SubmittedAt = attempt.SubmittedAt,
                        Score = attempt.Score,
                        Status = attempt.Status.ToString()
                    })
                    .ToList(),
                Questions = exam.ExamQuestions
                    .OrderBy(question => question.DisplayOrder)
                    .Select(question => new ExamQuestionManageViewModel
                    {
                        QuestionId = question.QuestionId,
                        Content = question.Question?.Content ?? "Cau hoi",
                        Score = question.Score,
                        OptionCount = question.Question?.Options.Count ?? 0
                    })
                    .ToList()
            };
        }

        private static ExamManageViewModel? BuildSampleManageModel(int id)
        {
            var card = BuildSampleExamCards().FirstOrDefault(exam => exam.Id == id && exam.IsOwnedByCurrentUser);
            if (card is null)
            {
                return null;
            }

            return new ExamManageViewModel
            {
                Id = card.Id,
                Title = card.Title,
                SubjectName = card.SubjectName,
                ClassName = card.ClassName,
                StartAt = card.StartAt,
                EndAt = card.EndAt,
                DurationMinutes = card.DurationMinutes,
                QuestionCount = card.QuestionCount,
                ParticipantCount = 32,
                AttemptCount = 18,
                SubmittedCount = 12,
                WarningCount = 3,
                AverageScore = 7.4m,
                Participants =
                {
                    new ExamParticipantViewModel { DisplayName = "Nguyen Van B", Email = "student01@example.com", Status = "Active" },
                    new ExamParticipantViewModel { DisplayName = "Tran Thi C", Email = "student02@example.com", Status = "Active" },
                    new ExamParticipantViewModel { DisplayName = "Le Van D", Email = "student03@example.com", Status = "Active" }
                },
                Attempts =
                {
                    new ExamAttemptSummaryViewModel { AttemptId = 1, StudentName = "Nguyen Van B", StartedAt = DateTime.Now.AddMinutes(-35), SubmittedAt = DateTime.Now.AddMinutes(-5), Score = 8.2m, Status = "Submitted" },
                    new ExamAttemptSummaryViewModel { AttemptId = 2, StudentName = "Tran Thi C", StartedAt = DateTime.Now.AddMinutes(-20), Score = null, Status = "InProgress" }
                },
                Questions =
                {
                    new ExamQuestionManageViewModel { QuestionId = 101, Content = "Trong ASP.NET Core MVC, thanh phan nao nhan request va tra ve response?", Score = 1, OptionCount = 4 },
                    new ExamQuestionManageViewModel { QuestionId = 102, Content = "Tag Helper nao giup bind input voi property trong model?", Score = 1, OptionCount = 4 }
                }
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

        private async Task<bool> OwnsClassAsync(int classId)
        {
            return await Context.Classes.AnyAsync(classRoom =>
                classRoom.Id == classId && classRoom.TeacherId == CurrentUserId);
        }

        private async Task<bool> CanManageExamAsync(int id)
        {
            if (IsAdmin)
            {
                return true;
            }

            if (id == 1 && !await Context.Exams.AnyAsync())
            {
                return true;
            }

            return await Context.Exams.AnyAsync(exam =>
                exam.Id == id
                && (exam.CreatedById == CurrentUserId || exam.Class.TeacherId == CurrentUserId));
        }

        private async Task<bool> CanEnterRoomAsync(int id)
        {
            if (id is 1 or 2 or 3 && !await Context.Exams.AnyAsync())
            {
                return id != 1;
            }

            return await Context.Exams.AnyAsync(exam =>
                exam.Id == id
                && exam.Class.Members.Any(member => member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active));
        }
    }
}
