using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccessPolicy _accessPolicy;
        private readonly IUniqueCodeGenerator _codeGenerator;
        private readonly IPrivateMediaStorage _mediaStorage;

        public ExamsController(
            ApplicationDbContext context,
            IAccessPolicy accessPolicy,
            IUniqueCodeGenerator codeGenerator,
            IPrivateMediaStorage mediaStorage)
        {
            _context = context;
            _accessPolicy = accessPolicy;
            _codeGenerator = codeGenerator;
            _mediaStorage = mediaStorage;
        }

        public async Task<IActionResult> Index()
        {
            var exams = await _context.Exams.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Class).Include(item => item.ExamQuestions)
                .Include(item => item.Attempts.Where(attempt => attempt.UserId == CurrentUserId))
                .Where(item => IsAdmin
                    || item.CreatedById == CurrentUserId
                    || item.Class.TeacherId == CurrentUserId
                    || (item.Status != ExamStatus.Draft && item.Status != ExamStatus.Cancelled
                        && item.Class.Members.Any(member => member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active)))
                .OrderByDescending(item => item.StartAt).Take(100).ToListAsync();

            return View(new ExamListViewModel
            {
                Exams = exams.Select(ToExamCard).ToList()
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return RedirectToAction("Index", "Classes");
        }

        [HttpGet]
        public async Task<IActionResult> CreateInClass(int classId)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(classId, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var classRoom = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId);
            if (classRoom is null)
            {
                return NotFound();
            }

            return View("Builder", new ExamBuilderViewModel
            {
                ClassId = classRoom.Id,
                ClassCode = classRoom.Code,
                ClassName = classRoom.Name,
                StartAt = DateTime.Now.AddHours(1),
                EndAt = DateTime.Now.AddHours(2),
                DurationMinutes = 45,
                MaxScore = 10,
                PassingScore = 5,
                MaxWarningCount = 3
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInClass(ExamBuilderViewModel model, string intent, CancellationToken cancellationToken)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(model.ClassId, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var classRoom = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.ClassId);
            if (classRoom is null)
            {
                return NotFound();
            }

            model.ClassCode = classRoom.Code;
            model.ClassName = classRoom.Name;
            var publish = string.Equals(intent, "publish", StringComparison.OrdinalIgnoreCase);
            PrepareBuilderModel(model, publish);
            if (!ModelState.IsValid)
            {
                return View("Builder", model);
            }

            var storedImages = new List<string>();
            try
            {
                var exam = new Exam
                {
                    ClassId = classRoom.Id,
                    SubjectId = classRoom.SubjectId,
                    CreatedById = CurrentUserId,
                    Code = await _codeGenerator.GenerateExamCodeAsync(cancellationToken),
                    Status = publish ? ExamStatus.Published : ExamStatus.Draft
                };
                ApplyExamSettings(exam, model);
                _context.Exams.Add(exam);
                await AddQuestionsAsync(exam, model, publish, storedImages, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                TempData["ExamMessage"] = publish ? "Đề thi đã được công bố." : "Bản nháp đã được lưu.";
                return publish
                    ? RedirectToAction(nameof(Manage), new { id = exam.Id })
                    : RedirectToAction(nameof(EditBuilder), new { id = exam.Id });
            }
            catch (InvalidOperationException exception)
            {
                foreach (var path in storedImages)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(string.Empty, exception.Message);
                return View("Builder", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditBuilder(int id, string panel = "questions")
        {
            if (!await _accessPolicy.CanManageExamAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var exam = await LoadExamForBuilderAsync(id);
            if (exam is null)
            {
                return NotFound();
            }

            var model = ToBuilderModel(exam);
            model.ActivePanel = panel == "settings" ? "settings" : "questions";
            model.IsLocked = exam.Attempts.Count > 0;
            return View("Builder", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBuilder(ExamBuilderViewModel model, string intent, CancellationToken cancellationToken)
        {
            if (!model.ExamId.HasValue || !await _accessPolicy.CanManageExamAsync(model.ExamId.Value, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var exam = await LoadExamForBuilderAsync(model.ExamId.Value);
            if (exam is null)
            {
                return NotFound();
            }

            model.ClassId = exam.ClassId;
            model.ClassCode = exam.Class.Code;
            model.ClassName = exam.Class.Name;
            model.ExamCode = exam.Code;
            model.IsLocked = exam.Attempts.Count > 0;
            if (model.IsLocked)
            {
                ModelState.AddModelError(string.Empty, "Đề đã có lượt thi nên nội dung và cấu hình không thể thay đổi.");
                return View("Builder", model);
            }

            var publish = string.Equals(intent, "publish", StringComparison.OrdinalIgnoreCase);
            PrepareBuilderModel(model, publish);
            if (!ModelState.IsValid)
            {
                return View("Builder", model);
            }

            var storedImages = new List<string>();
            var imagesToDelete = new List<string>();
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _context.Entry(exam).Property(item => item.RowVersion).OriginalValue = model.RowVersion;
                ApplyExamSettings(exam, model);
                exam.Status = publish ? ExamStatus.Published : ExamStatus.Draft;
                await ReplaceQuestionsAsync(exam, model, publish, storedImages, imagesToDelete, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                foreach (var path in imagesToDelete.Distinct())
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                TempData["ExamMessage"] = publish ? "Đề thi đã được cập nhật và công bố." : "Bản nháp đã được cập nhật.";
                return publish
                    ? RedirectToAction(nameof(Manage), new { id = exam.Id })
                    : RedirectToAction(nameof(EditBuilder), new { id = exam.Id, panel = model.ActivePanel });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                foreach (var path in storedImages)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(string.Empty, "Đề thi vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");
                return View("Builder", model);
            }
            catch (InvalidOperationException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                foreach (var path in storedImages)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(string.Empty, exception.Message);
                return View("Builder", model);
            }
        }

        public async Task<IActionResult> Manage(int id)
        {
            if (!await _accessPolicy.CanManageExamAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var exam = await _context.Exams.AsNoTracking()
                .Include(item => item.Subject)
                .Include(item => item.Class).ThenInclude(classRoom => classRoom.Members).ThenInclude(member => member.User)
                .Include(item => item.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.Options)
                .Include(item => item.Attempts).ThenInclude(attempt => attempt.User)
                .Include(item => item.Attempts).ThenInclude(attempt => attempt.AntiCheatEvents)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (exam is null)
            {
                return NotFound();
            }

            var submitted = exam.Attempts.Where(item => item.SubmittedAt.HasValue).ToList();
            var scored = submitted.Where(item => item.Score.HasValue).ToList();
            return View(new ExamManageViewModel
            {
                Id = exam.Id,
                ClassId = exam.ClassId,
                Code = exam.Code,
                Title = exam.Title,
                SubjectName = exam.Subject.Name,
                ClassName = exam.Class.Name,
                Status = exam.Status,
                ResultReleaseMode = exam.ResultReleaseMode,
                ResultsReleased = exam.ResultsReleasedAt.HasValue,
                CanEditContent = exam.Attempts.Count == 0,
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                DurationMinutes = exam.DurationMinutes,
                MaxScore = exam.MaxScore,
                MaxWarningCount = exam.MaxWarningCount,
                QuestionCount = exam.ExamQuestions.Count,
                ParticipantCount = exam.Class.Members.Count(member => member.Status == ClassMemberStatus.Active),
                AttemptCount = exam.Attempts.Count,
                SubmittedCount = submitted.Count,
                WarningCount = exam.Attempts.Sum(item => item.AntiCheatEvents.Count),
                AverageScore = scored.Count == 0 ? null : scored.Average(item => item.Score),
                Participants = exam.Class.Members.Where(member => member.Status == ClassMemberStatus.Active).Select(member => new ExamParticipantViewModel
                {
                    UserId = member.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email ?? "Học viên" : member.User.FullName,
                    Email = member.User.Email ?? string.Empty
                }).ToList(),
                Attempts = exam.Attempts.OrderByDescending(item => item.StartedAt).Select(item => new ExamAttemptSummaryViewModel
                {
                    AttemptId = item.Id,
                    StudentName = string.IsNullOrWhiteSpace(item.User.FullName) ? item.User.Email ?? "Học viên" : item.User.FullName,
                    StartedAt = item.StartedAt,
                    SubmittedAt = item.SubmittedAt,
                    Score = item.Score,
                    Status = item.Status,
                    WarningCount = item.AntiCheatEvents.Count
                }).ToList(),
                Questions = exam.ExamQuestions.OrderBy(item => item.DisplayOrder).Select(item => new ExamQuestionManageViewModel
                {
                    QuestionId = item.QuestionId,
                    Content = item.Question.Content,
                    Score = item.Score,
                    OptionCount = item.Question.Options.Count
                }).ToList(),
                Warnings = exam.Attempts.SelectMany(attempt => attempt.AntiCheatEvents.Select(item => new AntiCheatEventItemViewModel
                {
                    StudentName = string.IsNullOrWhiteSpace(attempt.User.FullName) ? attempt.User.Email ?? "Học viên" : attempt.User.FullName,
                    EventType = item.EventType,
                    OccurredAt = item.OccurredAt
                })).OrderByDescending(item => item.OccurredAt).Take(100).ToList()
            });
        }

        public async Task<IActionResult> Room(int id)
        {
            if (await _accessPolicy.CanManageExamAsync(id, CurrentUserId, IsAdmin))
            {
                return RedirectToAction(nameof(Manage), new { id });
            }

            if (!await _accessPolicy.CanTakeExamAsync(id, CurrentUserId))
            {
                return Forbid();
            }

            var completedAttempt = await _context.ExamAttempts
                .AsNoTracking()
                .Where(item => item.ExamId == id
                    && item.UserId == CurrentUserId
                    && (item.SubmittedAt.HasValue
                        || item.Status == ExamAttemptStatus.Submitted
                        || item.Status == ExamAttemptStatus.AutoSubmitted))
                .Select(item => new { item.Id })
                .FirstOrDefaultAsync();
            if (completedAttempt is not null)
            {
                return RedirectToAction(nameof(Result), new { attemptId = completedAttempt.Id });
            }

            var room = await BuildRoomModelAsync(id);
            if (room is null)
            {
                return NotFound();
            }

            return room.IsOpen ? View("Confirm", room) : View("Waiting", room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id, bool acceptRules)
        {
            if (!acceptRules)
            {
                ModelState.AddModelError(string.Empty, "Vui lòng xác nhận đã đọc quy định trước khi bắt đầu.");
                var invalidRoom = await BuildRoomModelAsync(id);
                return invalidRoom is null ? NotFound() : View("Confirm", invalidRoom);
            }

            if (!await _accessPolicy.CanTakeExamAsync(id, CurrentUserId))
            {
                return Forbid();
            }

            var exam = await _context.Exams.Include(item => item.Class).FirstOrDefaultAsync(item => item.Id == id);
            if (exam is null)
            {
                return NotFound();
            }

            if (DateTime.Now < exam.StartAt || DateTime.Now > exam.EndAt)
            {
                return RedirectToAction(nameof(Room), new { id });
            }

            var attempt = await _context.ExamAttempts.FirstOrDefaultAsync(item => item.ExamId == id && item.UserId == CurrentUserId);
            if (attempt?.SubmittedAt is not null)
            {
                return RedirectToAction(nameof(Result), new { attemptId = attempt.Id });
            }

            if (attempt is null)
            {
                attempt = new ExamAttempt
                {
                    ExamId = id,
                    UserId = CurrentUserId,
                    StartedAt = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                };
                _context.ExamAttempts.Add(attempt);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _context.Entry(attempt).State = EntityState.Detached;
                    attempt = await _context.ExamAttempts.SingleAsync(item => item.ExamId == id && item.UserId == CurrentUserId);
                }
            }

            return RedirectToAction(nameof(Take), new { attemptId = attempt.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Take(int attemptId)
        {
            var attempt = await LoadAttemptAsync(attemptId);
            if (attempt is null)
            {
                return NotFound();
            }

            if (!IsAdmin && attempt.UserId != CurrentUserId)
            {
                return Forbid();
            }

            if (attempt.SubmittedAt.HasValue)
            {
                return RedirectToAction(nameof(Result), new { attemptId });
            }

            if (HasExpired(attempt))
            {
                await SubmitAttemptAsync(attempt, true);
                return RedirectToAction(nameof(Result), new { attemptId });
            }

            return View("Start", BuildStartModel(attempt));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Autosave(AttemptAnswerInputModel model)
        {
            var attempt = await _context.ExamAttempts
                .Include(item => item.Exam).ThenInclude(exam => exam.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.Options)
                .Include(item => item.Answers).ThenInclude(answer => answer.Selections)
                .FirstOrDefaultAsync(item => item.Id == model.AttemptId);
            if (attempt is null || attempt.UserId != CurrentUserId)
            {
                return Forbid();
            }

            if (attempt.SubmittedAt.HasValue || HasExpired(attempt))
            {
                if (!attempt.SubmittedAt.HasValue)
                {
                    await SubmitAttemptAsync(attempt, true);
                }

                return Conflict(new { ok = false, locked = true, message = "Bài thi đã được khóa." });
            }

            var examQuestion = attempt.Exam.ExamQuestions.FirstOrDefault(item => item.QuestionId == model.QuestionId);
            if (examQuestion is null)
            {
                return BadRequest(new { ok = false, message = "Câu hỏi không thuộc đề thi." });
            }

            var selectedIds = model.SelectedOptionIds.Distinct().ToList();
            var validIds = examQuestion.Question.Options.Select(item => item.Id).ToHashSet();
            if (selectedIds.Any(id => !validIds.Contains(id)) || selectedIds.Count > 1)
            {
                return BadRequest(new { ok = false, message = "Đáp án không hợp lệ." });
            }

            var answer = attempt.Answers.FirstOrDefault(item => item.QuestionId == model.QuestionId);
            if (answer is null)
            {
                answer = new AttemptAnswer { ExamAttemptId = attempt.Id, QuestionId = model.QuestionId };
                attempt.Answers.Add(answer);
            }
            else
            {
                _context.AttemptAnswerSelections.RemoveRange(answer.Selections);
                answer.Selections.Clear();
            }

            foreach (var selectedId in selectedIds)
            {
                answer.Selections.Add(new AttemptAnswerSelection { QuestionOptionId = selectedId });
            }

            answer.LastSavedAt = DateTime.UtcNow;
            answer.IsCorrect = null;
            answer.AwardedScore = null;
            await _context.SaveChangesAsync();
            return Ok(new { ok = true, savedAt = DateTime.Now.ToString("HH:mm:ss") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(ExamSubmitViewModel model)
        {
            var attempt = await LoadAttemptAsync(model.AttemptId);
            if (attempt is null)
            {
                return NotFound();
            }

            if (attempt.UserId != CurrentUserId)
            {
                return Forbid();
            }

            if (!attempt.SubmittedAt.HasValue)
            {
                await SubmitAttemptAsync(attempt, HasExpired(attempt));
            }

            return RedirectToAction(nameof(Result), new { attemptId = attempt.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordWarning(AntiCheatEventInputModel model)
        {
            var attempt = await LoadAttemptAsync(model.AttemptId);
            if (attempt is null || attempt.UserId != CurrentUserId)
            {
                return Forbid();
            }

            if (attempt.SubmittedAt.HasValue)
            {
                return Conflict(new { locked = true });
            }

            var duplicateSince = DateTime.UtcNow.AddSeconds(-3);
            if (!attempt.AntiCheatEvents.Any(item => item.EventType == model.EventType && item.OccurredAt >= duplicateSince))
            {
                attempt.AntiCheatEvents.Add(new AntiCheatEvent
                {
                    EventType = model.EventType,
                    Severity = model.EventType == AntiCheatEventType.FullscreenExited ? AntiCheatSeverity.High : AntiCheatSeverity.Medium,
                    Description = WarningDescription(model.EventType)
                });
                await _context.SaveChangesAsync();
            }

            var warningCount = attempt.AntiCheatEvents.Count;
            var locked = attempt.Exam.MaxWarningCount.HasValue && warningCount >= attempt.Exam.MaxWarningCount.Value;
            if (locked)
            {
                await SubmitAttemptAsync(attempt, true);
            }

            return Ok(new { warningCount, maxWarningCount = attempt.Exam.MaxWarningCount, locked });
        }

        [HttpGet]
        public async Task<IActionResult> Result(int attemptId)
        {
            var attempt = await LoadAttemptAsync(attemptId);
            if (attempt is null)
            {
                return NotFound();
            }

            var canManage = await _accessPolicy.CanManageExamAsync(attempt.ExamId, CurrentUserId, IsAdmin);
            if (!canManage && attempt.UserId != CurrentUserId)
            {
                return Forbid();
            }

            var released = canManage || attempt.Exam.ResultReleaseMode == ResultReleaseMode.Immediately
                || (attempt.Exam.ResultReleaseMode == ResultReleaseMode.AfterExamClosed && DateTime.Now > attempt.Exam.EndAt)
                || (attempt.Exam.ResultReleaseMode == ResultReleaseMode.Manual && attempt.Exam.ResultsReleasedAt.HasValue);

            return View(new ExamResultViewModel
            {
                AttemptId = attempt.Id,
                ExamTitle = attempt.Exam.Title,
                ClassName = attempt.Exam.Class.Name,
                Score = released ? attempt.Score : null,
                MaxScore = attempt.Exam.MaxScore,
                PassingScore = attempt.Exam.PassingScore,
                CorrectCount = released ? attempt.Answers.Count(item => item.IsCorrect == true) : 0,
                QuestionCount = attempt.Exam.ExamQuestions.Count,
                IsReleased = released,
                IsAutoSubmitted = attempt.IsAutoSubmitted,
                SubmittedAt = attempt.SubmittedAt
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseResults(int id)
        {
            if (!await _accessPolicy.CanManageExamAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var exam = await _context.Exams.FirstOrDefaultAsync(item => item.Id == id);
            if (exam is null)
            {
                return NotFound();
            }

            exam.ResultsReleasedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["ExamMessage"] = "Kết quả đã được công bố cho học viên.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            if (!await _accessPolicy.CanManageExamAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var exam = await _context.Exams.FirstOrDefaultAsync(item => item.Id == id);
            if (exam is null)
            {
                return NotFound();
            }

            exam.Status = ExamStatus.Closed;
            await _context.SaveChangesAsync();
            TempData["ExamMessage"] = "Đề thi đã được đóng.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        private void PrepareBuilderModel(ExamBuilderViewModel model, bool publish)
        {
            model.Questions = model.Questions
                .Where(question => !string.IsNullOrWhiteSpace(question.Content)
                    || question.Options.Any(option => !string.IsNullOrWhiteSpace(option.Content)))
                .ToList();
            foreach (var question in model.Questions)
            {
                question.QuestionType = QuestionType.SingleChoice;
                question.Options = question.Options.Where(option => !string.IsNullOrWhiteSpace(option.Content)).ToList();
            }

            ModelState.Clear();
            TryValidateModel(model);
            if (!publish)
            {
                foreach (var key in ModelState.Keys.Where(key => key.StartsWith("Questions[", StringComparison.Ordinal)).ToList())
                {
                    ModelState.Remove(key);
                }
            }

            if (model.EndAt <= model.StartAt)
            {
                ModelState.AddModelError(nameof(model.EndAt), "Thời gian kết thúc phải sau thời gian bắt đầu.");
            }
            if (model.EndAt.Subtract(model.StartAt).TotalMinutes < model.DurationMinutes)
            {
                ModelState.AddModelError(nameof(model.DurationMinutes), "Khoảng thời gian mở đề phải đủ cho thời lượng làm bài.");
            }
            if (model.PassingScore.HasValue && model.PassingScore > model.MaxScore)
            {
                ModelState.AddModelError(nameof(model.PassingScore), "Điểm đạt không được lớn hơn điểm tối đa.");
            }
            ValidateVideoUrls(model);
            if (publish)
            {
                ValidateQuestions(model);
            }
        }

        private void ValidateQuestions(ExamBuilderViewModel model)
        {
            if (model.Questions.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Đề thi cần ít nhất một câu hỏi.");
                return;
            }

            for (var index = 0; index < model.Questions.Count; index++)
            {
                var question = model.Questions[index];
                if (string.IsNullOrWhiteSpace(question.Content))
                {
                    ModelState.AddModelError($"Questions[{index}].Content", "Vui lòng nhập nội dung câu hỏi.");
                }
                if (question.Options.Count < 2)
                {
                    ModelState.AddModelError($"Questions[{index}].Options", "Mỗi câu cần ít nhất hai đáp án.");
                }

                var correctCount = question.Options.Count(option => option.IsCorrect);
                if (correctCount != 1)
                {
                    ModelState.AddModelError($"Questions[{index}].Options", "Mỗi câu phải có đúng một đáp án đúng.");
                }
            }

            var assigned = model.Questions.Where(item => item.Score.HasValue).Sum(item => item.Score!.Value);
            var autoCount = model.Questions.Count(item => !item.Score.HasValue);
            if (assigned > model.MaxScore || (autoCount > 0 && assigned >= model.MaxScore))
            {
                ModelState.AddModelError(nameof(model.MaxScore), "Tổng điểm đã nhập phải nhỏ hơn điểm tối đa khi còn câu tự chia điểm.");
            }
            if (autoCount == 0 && assigned != model.MaxScore)
            {
                ModelState.AddModelError(nameof(model.MaxScore), "Tổng điểm các câu phải bằng điểm tối đa của đề.");
            }
        }

        private void ValidateVideoUrls(ExamBuilderViewModel model)
        {
            for (var index = 0; index < model.Questions.Count; index++)
            {
                var value = model.Questions[index].VideoUrl;
                if (!string.IsNullOrWhiteSpace(value)
                    && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError($"Questions[{index}].VideoUrl", "Video phải dùng liên kết HTTPS hợp lệ.");
                }
            }
        }

        private void ApplyExamSettings(Exam exam, ExamBuilderViewModel model)
        {
            exam.Title = model.Title.Trim();
            exam.Instructions = NormalizeOptional(model.Instructions);
            exam.DurationMinutes = model.DurationMinutes;
            exam.StartAt = model.StartAt;
            exam.EndAt = model.EndAt;
            exam.MaxScore = model.MaxScore;
            exam.PassingScore = model.PassingScore;
            exam.ShuffleQuestions = model.ShuffleQuestions;
            exam.ShuffleOptions = model.ShuffleOptions;
            exam.RequireFullscreen = model.RequireFullscreen;
            exam.MaxWarningCount = model.MaxWarningCount;
            exam.ResultReleaseMode = model.ResultReleaseMode;
        }

        private async Task AddQuestionsAsync(
            Exam exam,
            ExamBuilderViewModel model,
            bool publish,
            IList<string> storedImages,
            CancellationToken cancellationToken)
        {
            var scores = CalculateScores(model, publish);
            for (var index = 0; index < model.Questions.Count; index++)
            {
                var input = model.Questions[index];
                var mediaAssets = await BuildQuestionMediaAsync(input, null, storedImages, null, false, cancellationToken);
                var imagePath = mediaAssets
                    .Where(media => media.MediaType == MediaAssetType.Image)
                    .OrderBy(media => media.DisplayOrder)
                    .Select(media => media.Path)
                    .FirstOrDefault();
                var question = BuildQuestion(input, exam.SubjectId, imagePath);
                foreach (var media in mediaAssets)
                {
                    question.MediaAssets.Add(media);
                }

                exam.ExamQuestions.Add(new ExamQuestion
                {
                    Question = question,
                    DisplayOrder = index + 1,
                    Score = scores[index]
                });
            }
        }

        private async Task ReplaceQuestionsAsync(
            Exam exam,
            ExamBuilderViewModel model,
            bool publish,
            IList<string> storedImages,
            IList<string> imagesToDelete,
            CancellationToken cancellationToken)
        {
            var existingLinks = exam.ExamQuestions.ToList();
            var existingById = existingLinks.ToDictionary(item => item.QuestionId, item => item.Question);
            var retainedQuestionIds = model.Questions
                .Where(item => item.QuestionId.HasValue)
                .Select(item => item.QuestionId!.Value)
                .ToHashSet();
            _context.ExamQuestions.RemoveRange(existingLinks);

            foreach (var link in existingLinks)
            {
                var question = link.Question;
                var usedElsewhere = question.ExamQuestions.Any(item => item.ExamId != exam.Id);
                if (!usedElsewhere && question.AttemptAnswers.Count == 0)
                {
                    if (!retainedQuestionIds.Contains(question.Id))
                    {
                        if (question.ImagePath is not null)
                        {
                            imagesToDelete.Add(question.ImagePath);
                        }

                        foreach (var media in question.MediaAssets)
                        {
                            imagesToDelete.Add(media.Path);
                        }
                    }

                    _context.Questions.Remove(question);
                }
            }

            exam.ExamQuestions.Clear();
            var scores = CalculateScores(model, publish);
            for (var index = 0; index < model.Questions.Count; index++)
            {
                var input = model.Questions[index];
                existingById.TryGetValue(input.QuestionId ?? 0, out var oldQuestion);
                var oldQuestionUsedElsewhere = oldQuestion?.ExamQuestions.Any(item => item.ExamId != exam.Id) == true;
                var mediaAssets = await BuildQuestionMediaAsync(input, oldQuestion, storedImages, imagesToDelete, oldQuestionUsedElsewhere, cancellationToken);
                var imagePath = mediaAssets
                    .Where(media => media.MediaType == MediaAssetType.Image)
                    .OrderBy(media => media.DisplayOrder)
                    .Select(media => media.Path)
                    .FirstOrDefault();
                var question = BuildQuestion(input, exam.SubjectId, imagePath);
                foreach (var media in mediaAssets)
                {
                    question.MediaAssets.Add(media);
                }

                exam.ExamQuestions.Add(new ExamQuestion
                {
                    Question = question,
                    DisplayOrder = index + 1,
                    Score = scores[index]
                });
            }
        }

        private async Task<IList<QuestionMedia>> BuildQuestionMediaAsync(
            ExamBuilderQuestionInputModel input,
            Question? oldQuestion,
            IList<string> storedMediaPaths,
            IList<string>? mediaToDelete,
            bool oldQuestionUsedElsewhere,
            CancellationToken cancellationToken)
        {
            var mediaAssets = new List<QuestionMedia>();
            var removeIds = input.RemoveMediaIds.Distinct().ToHashSet();
            var oldMediaAssets = oldQuestion?.MediaAssets
                .OrderBy(media => media.MediaType)
                .ThenBy(media => media.DisplayOrder)
                .ToList() ?? new List<QuestionMedia>();

            foreach (var oldMedia in oldMediaAssets)
            {
                if (removeIds.Contains(oldMedia.Id))
                {
                    if (!oldQuestionUsedElsewhere)
                    {
                        mediaToDelete?.Add(oldMedia.Path);
                    }

                    continue;
                }

                mediaAssets.Add(CloneQuestionMedia(oldMedia, NextQuestionMediaOrder(mediaAssets, oldMedia.MediaType)));
            }

            if (oldQuestion?.ImagePath is not null && oldMediaAssets.All(media => media.Path != oldQuestion.ImagePath))
            {
                if (input.RemoveImage)
                {
                    if (!oldQuestionUsedElsewhere)
                    {
                        mediaToDelete?.Add(oldQuestion.ImagePath);
                    }
                }
                else
                {
                    mediaAssets.Add(new QuestionMedia
                    {
                        MediaType = MediaAssetType.Image,
                        Path = oldQuestion.ImagePath,
                        DisplayOrder = NextQuestionMediaOrder(mediaAssets, MediaAssetType.Image)
                    });
                }
            }

            var imageFiles = GetFiles(input.ImageFiles);
            if (input.ImageFile is not null && imageFiles.Count == 0)
            {
                imageFiles.Add(input.ImageFile);
            }

            var videoFiles = GetFiles(input.VideoFiles);
            ValidateQuestionMediaLimit(mediaAssets, imageFiles.Count, videoFiles.Count);

            foreach (var file in imageFiles)
            {
                var path = await _mediaStorage.SaveImageAsync(file, "questions", cancellationToken);
                storedMediaPaths.Add(path);
                mediaAssets.Add(new QuestionMedia
                {
                    MediaType = MediaAssetType.Image,
                    Path = path,
                    OriginalFileName = file.FileName,
                    DisplayOrder = NextQuestionMediaOrder(mediaAssets, MediaAssetType.Image)
                });
            }

            foreach (var file in videoFiles)
            {
                var path = await _mediaStorage.SaveVideoAsync(file, "questions", cancellationToken);
                storedMediaPaths.Add(path);
                mediaAssets.Add(new QuestionMedia
                {
                    MediaType = MediaAssetType.Video,
                    Path = path,
                    OriginalFileName = file.FileName,
                    DisplayOrder = NextQuestionMediaOrder(mediaAssets, MediaAssetType.Video)
                });
            }

            return mediaAssets;
        }

        private static QuestionMedia CloneQuestionMedia(QuestionMedia media, int displayOrder)
        {
            return new QuestionMedia
            {
                MediaType = media.MediaType,
                Path = media.Path,
                OriginalFileName = media.OriginalFileName,
                DisplayOrder = displayOrder
            };
        }

        private static List<IFormFile> GetFiles(IFormFileCollection? files)
        {
            return files?.Where(file => file is not null && file.Length > 0).ToList() ?? new List<IFormFile>();
        }

        private static void ValidateQuestionMediaLimit(IEnumerable<QuestionMedia> existingMedia, int newImageCount, int newVideoCount)
        {
            var existingImages = existingMedia.Count(media => media.MediaType == MediaAssetType.Image);
            var existingVideos = existingMedia.Count(media => media.MediaType == MediaAssetType.Video);
            if (existingImages + newImageCount > 5)
            {
                throw new InvalidOperationException("Mỗi câu hỏi chỉ được lưu tối đa 5 ảnh.");
            }

            if (existingVideos + newVideoCount > 2)
            {
                throw new InvalidOperationException("Mỗi câu hỏi chỉ được lưu tối đa 2 video.");
            }
        }

        private static int NextQuestionMediaOrder(IEnumerable<QuestionMedia> mediaAssets, MediaAssetType mediaType)
        {
            var ordered = mediaAssets.Where(media => media.MediaType == mediaType).ToList();
            return ordered.Count == 0 ? 1 : ordered.Max(media => media.DisplayOrder) + 1;
        }

        private Question BuildQuestion(ExamBuilderQuestionInputModel input, int subjectId, string? imagePath)
        {
            var question = new Question
            {
                SubjectId = subjectId,
                CreatedById = CurrentUserId,
                Content = input.Content.Trim(),
                ImagePath = imagePath,
                VideoUrl = NormalizeOptional(input.VideoUrl),
                QuestionType = QuestionType.SingleChoice,
                Difficulty = input.Difficulty,
                Explanation = NormalizeOptional(input.Explanation),
                Status = QuestionStatus.Published
            };
            for (var index = 0; index < input.Options.Count; index++)
            {
                question.Options.Add(new QuestionOption
                {
                    Content = input.Options[index].Content.Trim(),
                    IsCorrect = input.Options[index].IsCorrect,
                    DisplayOrder = index + 1
                });
            }

            return question;
        }

        private static IList<decimal> CalculateScores(ExamBuilderViewModel model, bool publish)
        {
            if (!publish)
            {
                return model.Questions.Select(item => item.Score ?? 0).ToList();
            }

            var result = model.Questions.Select(item => item.Score ?? 0).ToList();
            var autoIndexes = model.Questions.Select((item, index) => new { item.Score, index })
                .Where(item => !item.Score.HasValue).Select(item => item.index).ToList();
            if (autoIndexes.Count == 0)
            {
                return result;
            }

            var remaining = model.MaxScore - result.Sum();
            var normalShare = Math.Round(remaining / autoIndexes.Count, 2, MidpointRounding.AwayFromZero);
            for (var index = 0; index < autoIndexes.Count - 1; index++)
            {
                result[autoIndexes[index]] = normalShare;
                remaining -= normalShare;
            }
            result[autoIndexes[^1]] = remaining;
            return result;
        }

        private async Task<Exam?> LoadExamForBuilderAsync(int id)
        {
            return await _context.Exams
                .Include(item => item.Class)
                .Include(item => item.Attempts)
                .Include(item => item.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.Options)
                .Include(item => item.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.MediaAssets)
                .Include(item => item.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.ExamQuestions)
                .Include(item => item.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.AttemptAnswers)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        private ExamBuilderViewModel ToBuilderModel(Exam exam)
        {
            return new ExamBuilderViewModel
            {
                ExamId = exam.Id,
                ClassId = exam.ClassId,
                ClassCode = exam.Class.Code,
                ClassName = exam.Class.Name,
                ExamCode = exam.Code,
                Title = exam.Title,
                Instructions = exam.Instructions,
                DurationMinutes = exam.DurationMinutes,
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                MaxScore = exam.MaxScore,
                PassingScore = exam.PassingScore,
                ShuffleQuestions = exam.ShuffleQuestions,
                ShuffleOptions = exam.ShuffleOptions,
                RequireFullscreen = exam.RequireFullscreen,
                MaxWarningCount = exam.MaxWarningCount,
                ResultReleaseMode = exam.ResultReleaseMode,
                RowVersion = exam.RowVersion,
                Questions = exam.ExamQuestions.OrderBy(item => item.DisplayOrder).Select(item => new ExamBuilderQuestionInputModel
                {
                    QuestionId = item.QuestionId,
                    Content = item.Question.Content,
                    QuestionType = item.Question.QuestionType,
                    Score = item.Score > 0 ? item.Score : null,
                    Difficulty = item.Question.Difficulty,
                    ExistingImageUrl = item.Question.ImagePath is null ? null : Url.Action("QuestionImage", "Media", new { id = item.QuestionId }),
                    ExistingMedia = item.Question.MediaAssets
                        .OrderBy(media => media.MediaType)
                        .ThenBy(media => media.DisplayOrder)
                        .Select(media => new MediaAssetViewModel
                        {
                            Id = media.Id,
                            MediaType = media.MediaType,
                            FileName = media.OriginalFileName,
                            Url = Url.Action("QuestionMedia", "Media", new { id = item.QuestionId, mediaId = media.Id }) ?? string.Empty
                        }).ToList(),
                    VideoUrl = item.Question.VideoUrl,
                    Explanation = item.Question.Explanation,
                    Options = item.Question.Options.OrderBy(option => option.DisplayOrder).Select(option => new ExamBuilderOptionInputModel
                    {
                        OptionId = option.Id,
                        Content = option.Content,
                        IsCorrect = option.IsCorrect
                    }).ToList()
                }).DefaultIfEmpty(ExamBuilderQuestionInputModel.CreateDefault()).ToList()
            };
        }

        private async Task<ExamRoomViewModel?> BuildRoomModelAsync(int id)
        {
            var exam = await _context.Exams.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Class).Include(item => item.ExamQuestions)
                .FirstOrDefaultAsync(item => item.Id == id);
            return exam is null ? null : new ExamRoomViewModel
            {
                Id = exam.Id,
                Code = exam.Code,
                Title = exam.Title,
                SubjectName = exam.Subject.Name,
                ClassName = exam.Class.Name,
                CoverImageUrl = exam.Class.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = exam.ClassId }),
                Instructions = exam.Instructions,
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                DurationMinutes = exam.DurationMinutes,
                QuestionCount = exam.ExamQuestions.Count,
                MaxScore = exam.MaxScore,
                RequireFullscreen = exam.RequireFullscreen,
                MaxWarningCount = exam.MaxWarningCount,
                Now = DateTime.Now
            };
        }

        private async Task<ExamAttempt?> LoadAttemptAsync(int attemptId)
        {
            return await _context.ExamAttempts
                .Include(item => item.Exam).ThenInclude(exam => exam.Class)
                .Include(item => item.Exam).ThenInclude(exam => exam.Subject)
                .Include(item => item.Exam).ThenInclude(exam => exam.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.Options)
                .Include(item => item.Exam).ThenInclude(exam => exam.ExamQuestions).ThenInclude(link => link.Question).ThenInclude(question => question.MediaAssets)
                .Include(item => item.Answers).ThenInclude(answer => answer.Selections)
                .Include(item => item.AntiCheatEvents)
                .FirstOrDefaultAsync(item => item.Id == attemptId);
        }

        private ExamStartViewModel BuildStartModel(ExamAttempt attempt)
        {
            var questionLinks = attempt.Exam.ExamQuestions.AsEnumerable();
            if (attempt.Exam.ShuffleQuestions)
            {
                questionLinks = questionLinks.OrderBy(item => StableOrder(attempt.Id, item.QuestionId, "question"));
            }
            else
            {
                questionLinks = questionLinks.OrderBy(item => item.DisplayOrder);
            }

            var answers = attempt.Answers.ToDictionary(item => item.QuestionId);
            return new ExamStartViewModel
            {
                ExamId = attempt.ExamId,
                AttemptId = attempt.Id,
                Title = attempt.Exam.Title,
                SubjectName = attempt.Exam.Subject.Name,
                DurationMinutes = attempt.Exam.DurationMinutes,
                ExpiresAt = GetExpiresAt(attempt),
                RequireFullscreen = attempt.Exam.RequireFullscreen,
                WarningCount = attempt.AntiCheatEvents.Count,
                MaxWarningCount = attempt.Exam.MaxWarningCount,
                Questions = questionLinks.Select(link =>
                {
                    var options = link.Question.Options.AsEnumerable();
                    options = attempt.Exam.ShuffleOptions
                        ? options.OrderBy(item => StableOrder(attempt.Id, item.Id, $"option-{link.QuestionId}"))
                        : options.OrderBy(item => item.DisplayOrder);
                    answers.TryGetValue(link.QuestionId, out var answer);
                    return new ExamQuestionViewModel
                    {
                        QuestionId = link.QuestionId,
                        Content = link.Question.Content,
                        ImageUrl = link.Question.ImagePath is null ? null : Url.Action("QuestionImage", "Media", new { id = link.QuestionId }),
                        VideoUrl = link.Question.VideoUrl,
                        MediaAssets = link.Question.MediaAssets
                            .OrderBy(media => media.MediaType)
                            .ThenBy(media => media.DisplayOrder)
                            .Select(media => new MediaAssetViewModel
                            {
                                Id = media.Id,
                                MediaType = media.MediaType,
                                FileName = media.OriginalFileName,
                                Url = Url.Action("QuestionMedia", "Media", new { id = link.QuestionId, mediaId = media.Id }) ?? string.Empty
                            }).ToList(),
                        QuestionType = link.Question.QuestionType,
                        Score = link.Score,
                        SelectedOptionIds = answer?.Selections.Select(item => item.QuestionOptionId).ToList() ?? new List<int>(),
                        Options = options.Select(item => new ExamOptionViewModel { OptionId = item.Id, Content = item.Content }).ToList()
                    };
                }).ToList()
            };
        }

        private async Task SubmitAttemptAsync(ExamAttempt attempt, bool automatic)
        {
            if (attempt.SubmittedAt.HasValue)
            {
                return;
            }

            var answers = attempt.Answers.ToDictionary(item => item.QuestionId);
            decimal total = 0;
            foreach (var link in attempt.Exam.ExamQuestions)
            {
                if (!answers.TryGetValue(link.QuestionId, out var answer))
                {
                    continue;
                }

                var selected = answer.Selections.Select(item => item.QuestionOptionId).OrderBy(item => item).ToArray();
                var correct = link.Question.Options.Where(item => item.IsCorrect).Select(item => item.Id).OrderBy(item => item).ToArray();
                var isCorrect = selected.SequenceEqual(correct);
                answer.IsCorrect = isCorrect;
                answer.AwardedScore = isCorrect ? link.Score : 0;
                total += answer.AwardedScore.Value;
            }

            attempt.Score = total;
            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.IsAutoSubmitted = automatic;
            attempt.Status = automatic ? ExamAttemptStatus.AutoSubmitted : ExamAttemptStatus.Submitted;
            await _context.SaveChangesAsync();
        }

        private ExamCardViewModel ToExamCard(Exam item)
        {
            var owns = IsAdmin || item.CreatedById == CurrentUserId || item.Class.TeacherId == CurrentUserId;
            var attempt = item.Attempts
                .Where(attempt => attempt.UserId == CurrentUserId)
                .OrderByDescending(attempt => attempt.StartedAt)
                .FirstOrDefault();
            return new ExamCardViewModel
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
                IsOwnedByCurrentUser = owns,
                IsParticipant = !owns,
                CurrentUserAttemptId = attempt?.Id,
                CurrentUserSubmittedAt = attempt?.SubmittedAt,
                CurrentUserAttemptStatus = attempt?.Status
            };
        }

        private static DateTime GetExpiresAt(ExamAttempt attempt)
        {
            var durationEnd = attempt.StartedAt.ToLocalTime().AddMinutes(attempt.Exam.DurationMinutes);
            return durationEnd < attempt.Exam.EndAt ? durationEnd : attempt.Exam.EndAt;
        }

        private static bool HasExpired(ExamAttempt attempt) => DateTime.Now >= GetExpiresAt(attempt);

        private static string StableOrder(int attemptId, int entityId, string scope)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{attemptId}:{scope}:{entityId}")));
        }

        private static string WarningDescription(AntiCheatEventType type) => type switch
        {
            AntiCheatEventType.TabHidden => "Rời khỏi tab làm bài",
            AntiCheatEventType.WindowBlur => "Cửa sổ làm bài mất tập trung",
            AntiCheatEventType.FullscreenExited => "Thoát chế độ toàn màn hình",
            AntiCheatEventType.CopyAttempt => "Thực hiện thao tác sao chép",
            AntiCheatEventType.PasteAttempt => "Thực hiện thao tác dán",
            _ => "Ghi nhận thay đổi trong phiên làm bài"
        };

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private bool IsAdmin => User.IsInRole("Admin");

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }
}
