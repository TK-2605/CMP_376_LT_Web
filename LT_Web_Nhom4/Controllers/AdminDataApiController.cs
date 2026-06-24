using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Controllers
{
    [ApiController]
    [ApiTestOnly]
    [Route("api/admin-data")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class AdminDataApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUniqueCodeGenerator _codeGenerator;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public AdminDataApiController(
            ApplicationDbContext context,
            IUniqueCodeGenerator codeGenerator,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<DatabaseSummaryResponse>> Summary(CancellationToken cancellationToken)
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            var tables = new List<DatabaseSummaryTable>
            {
                await CountAsync("users", () => _context.Users.CountAsync(cancellationToken)),
                await CountAsync("roles", () => _context.Roles.CountAsync(cancellationToken)),
                await CountAsync("subjects", () => _context.Subjects.CountAsync(cancellationToken)),
                await CountAsync("classes", () => _context.Classes.CountAsync(cancellationToken)),
                await CountAsync("classMembers", () => _context.ClassMembers.CountAsync(cancellationToken)),
                await CountAsync("questions", () => _context.Questions.CountAsync(cancellationToken)),
                await CountAsync("questionOptions", () => _context.QuestionOptions.CountAsync(cancellationToken)),
                await CountAsync("exams", () => _context.Exams.CountAsync(cancellationToken)),
                await CountAsync("examQuestions", () => _context.ExamQuestions.CountAsync(cancellationToken)),
                await CountAsync("examAttempts", () => _context.ExamAttempts.CountAsync(cancellationToken)),
                await CountAsync("attemptAnswers", () => _context.AttemptAnswers.CountAsync(cancellationToken)),
                await CountAsync("antiCheatEvents", () => _context.AntiCheatEvents.CountAsync(cancellationToken))
            };

            return Ok(new DatabaseSummaryResponse
            {
                Provider = _configuration["Database:Provider"] ?? "SqlServer",
                Environment = _environment.EnvironmentName,
                CanConnect = canConnect,
                Tables = tables
            });
        }

        [HttpGet("subjects")]
        public async Task<ActionResult<IReadOnlyList<SubjectDto>>> GetSubjects(CancellationToken cancellationToken)
        {
            var items = await _context.Subjects.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(200)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("subjects/{id:int}")]
        public async Task<ActionResult<SubjectDto>> GetSubject(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Subjects.AsNoTracking()
                .Where(subject => subject.Id == id)
                .Select(subject => ToDto(subject))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("subjects")]
        public async Task<IActionResult> CreateSubject(SubjectUpsertRequest request, CancellationToken cancellationToken)
        {
            var subject = new Subject
            {
                Code = request.Code.Trim(),
                Name = request.Name.Trim(),
                Description = NormalizeOptional(request.Description)
            };

            _context.Subjects.Add(subject);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetSubject), new { id = subject.Id }, ToDto(subject));
        }

        [HttpPut("subjects/{id:int}")]
        public async Task<IActionResult> UpdateSubject(int id, SubjectUpsertRequest request, CancellationToken cancellationToken)
        {
            var subject = await _context.Subjects.FindAsync([id], cancellationToken);
            if (subject is null)
            {
                return NotFound();
            }

            subject.Code = request.Code.Trim();
            subject.Name = request.Name.Trim();
            subject.Description = NormalizeOptional(request.Description);

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(subject));
        }

        [HttpDelete("subjects/{id:int}")]
        public async Task<IActionResult> DeleteSubject(int id, CancellationToken cancellationToken)
        {
            var subject = await _context.Subjects.FindAsync([id], cancellationToken);
            if (subject is null)
            {
                return NotFound();
            }

            _context.Subjects.Remove(subject);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("classes")]
        public async Task<ActionResult<IReadOnlyList<ClassDto>>> GetClasses(CancellationToken cancellationToken)
        {
            var items = await _context.Classes.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(200)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("classes/{id:int}")]
        public async Task<ActionResult<ClassDto>> GetClass(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Classes.AsNoTracking()
                .Where(classRoom => classRoom.Id == id)
                .Select(classRoom => ToDto(classRoom))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("classes")]
        public async Task<IActionResult> CreateClass(ClassUpsertRequest request, CancellationToken cancellationToken)
        {
            var classRoom = new ClassEntity
            {
                SubjectId = request.SubjectId,
                TeacherId = string.IsNullOrWhiteSpace(request.TeacherId) ? CurrentUserId : request.TeacherId.Trim(),
                Code = string.IsNullOrWhiteSpace(request.Code)
                    ? await _codeGenerator.GenerateClassCodeAsync(cancellationToken)
                    : request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                Description = NormalizeOptional(request.Description),
                Semester = NormalizeOptional(request.Semester),
                AcademicYear = NormalizeOptional(request.AcademicYear),
                IntroVideoUrl = NormalizeOptional(request.IntroVideoUrl)
            };

            _context.Classes.Add(classRoom);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetClass), new { id = classRoom.Id }, ToDto(classRoom));
        }

        [HttpPut("classes/{id:int}")]
        public async Task<IActionResult> UpdateClass(int id, ClassUpsertRequest request, CancellationToken cancellationToken)
        {
            var classRoom = await _context.Classes.FindAsync([id], cancellationToken);
            if (classRoom is null)
            {
                return NotFound();
            }

            classRoom.SubjectId = request.SubjectId;
            classRoom.TeacherId = string.IsNullOrWhiteSpace(request.TeacherId) ? classRoom.TeacherId : request.TeacherId.Trim();
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                classRoom.Code = request.Code.Trim().ToUpperInvariant();
            }

            classRoom.Name = request.Name.Trim();
            classRoom.Description = NormalizeOptional(request.Description);
            classRoom.Semester = NormalizeOptional(request.Semester);
            classRoom.AcademicYear = NormalizeOptional(request.AcademicYear);
            classRoom.IntroVideoUrl = NormalizeOptional(request.IntroVideoUrl);

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(classRoom));
        }

        [HttpDelete("classes/{id:int}")]
        public async Task<IActionResult> DeleteClass(int id, CancellationToken cancellationToken)
        {
            var classRoom = await _context.Classes.FindAsync([id], cancellationToken);
            if (classRoom is null)
            {
                return NotFound();
            }

            _context.Classes.Remove(classRoom);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("class-members")]
        public async Task<ActionResult<IReadOnlyList<ClassMemberDto>>> GetClassMembers(CancellationToken cancellationToken)
        {
            var items = await _context.ClassMembers.AsNoTracking()
                .OrderBy(item => item.ClassId)
                .ThenBy(item => item.UserId)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpPost("class-members")]
        public async Task<IActionResult> CreateClassMember(ClassMemberUpsertRequest request, CancellationToken cancellationToken)
        {
            if (await _context.ClassMembers.FindAsync([request.ClassId, request.UserId], cancellationToken) is not null)
            {
                return Conflict(new { message = "Class member already exists." });
            }

            var member = new ClassMember
            {
                ClassId = request.ClassId,
                UserId = request.UserId.Trim(),
                Status = request.Status
            };

            _context.ClassMembers.Add(member);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetClassMembers), ToDto(member));
        }

        [HttpPut("class-members/{classId:int}/{userId}")]
        public async Task<IActionResult> UpdateClassMember(int classId, string userId, ClassMemberUpsertRequest request, CancellationToken cancellationToken)
        {
            var member = await _context.ClassMembers.FindAsync([classId, userId], cancellationToken);
            if (member is null)
            {
                return NotFound();
            }

            member.Status = request.Status;
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(member));
        }

        [HttpDelete("class-members/{classId:int}/{userId}")]
        public async Task<IActionResult> DeleteClassMember(int classId, string userId, CancellationToken cancellationToken)
        {
            var member = await _context.ClassMembers.FindAsync([classId, userId], cancellationToken);
            if (member is null)
            {
                return NotFound();
            }

            _context.ClassMembers.Remove(member);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("questions")]
        public async Task<ActionResult<IReadOnlyList<QuestionDto>>> GetQuestions(CancellationToken cancellationToken)
        {
            var items = await _context.Questions.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(200)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("questions/{id:int}")]
        public async Task<ActionResult<QuestionDto>> GetQuestion(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Questions.AsNoTracking()
                .Where(question => question.Id == id)
                .Select(question => ToDto(question))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("questions")]
        public async Task<IActionResult> CreateQuestion(QuestionUpsertRequest request, CancellationToken cancellationToken)
        {
            var question = new Question
            {
                SubjectId = request.SubjectId,
                CreatedById = string.IsNullOrWhiteSpace(request.CreatedById) ? CurrentUserId : request.CreatedById.Trim(),
                Content = request.Content.Trim(),
                VideoUrl = NormalizeOptional(request.VideoUrl),
                QuestionType = request.QuestionType,
                Difficulty = request.Difficulty,
                Explanation = NormalizeOptional(request.Explanation),
                Status = request.Status
            };

            _context.Questions.Add(question);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetQuestion), new { id = question.Id }, ToDto(question));
        }

        [HttpPut("questions/{id:int}")]
        public async Task<IActionResult> UpdateQuestion(int id, QuestionUpsertRequest request, CancellationToken cancellationToken)
        {
            var question = await _context.Questions.FindAsync([id], cancellationToken);
            if (question is null)
            {
                return NotFound();
            }

            question.SubjectId = request.SubjectId;
            question.CreatedById = string.IsNullOrWhiteSpace(request.CreatedById) ? question.CreatedById : request.CreatedById.Trim();
            question.Content = request.Content.Trim();
            question.VideoUrl = NormalizeOptional(request.VideoUrl);
            question.QuestionType = request.QuestionType;
            question.Difficulty = request.Difficulty;
            question.Explanation = NormalizeOptional(request.Explanation);
            question.Status = request.Status;
            question.UpdatedAt = DateTime.UtcNow;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(question));
        }

        [HttpDelete("questions/{id:int}")]
        public async Task<IActionResult> DeleteQuestion(int id, CancellationToken cancellationToken)
        {
            var question = await _context.Questions.FindAsync([id], cancellationToken);
            if (question is null)
            {
                return NotFound();
            }

            _context.Questions.Remove(question);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("question-options")]
        public async Task<ActionResult<IReadOnlyList<QuestionOptionDto>>> GetQuestionOptions(CancellationToken cancellationToken)
        {
            var items = await _context.QuestionOptions.AsNoTracking()
                .OrderBy(item => item.QuestionId)
                .ThenBy(item => item.DisplayOrder)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("question-options/{id:int}")]
        public async Task<ActionResult<QuestionOptionDto>> GetQuestionOption(int id, CancellationToken cancellationToken)
        {
            var item = await _context.QuestionOptions.AsNoTracking()
                .Where(option => option.Id == id)
                .Select(option => ToDto(option))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("question-options")]
        public async Task<IActionResult> CreateQuestionOption(QuestionOptionUpsertRequest request, CancellationToken cancellationToken)
        {
            var option = new QuestionOption
            {
                QuestionId = request.QuestionId,
                Content = request.Content.Trim(),
                IsCorrect = request.IsCorrect,
                DisplayOrder = request.DisplayOrder
            };

            _context.QuestionOptions.Add(option);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetQuestionOption), new { id = option.Id }, ToDto(option));
        }

        [HttpPut("question-options/{id:int}")]
        public async Task<IActionResult> UpdateQuestionOption(int id, QuestionOptionUpsertRequest request, CancellationToken cancellationToken)
        {
            var option = await _context.QuestionOptions.FindAsync([id], cancellationToken);
            if (option is null)
            {
                return NotFound();
            }

            option.QuestionId = request.QuestionId;
            option.Content = request.Content.Trim();
            option.IsCorrect = request.IsCorrect;
            option.DisplayOrder = request.DisplayOrder;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(option));
        }

        [HttpDelete("question-options/{id:int}")]
        public async Task<IActionResult> DeleteQuestionOption(int id, CancellationToken cancellationToken)
        {
            var option = await _context.QuestionOptions.FindAsync([id], cancellationToken);
            if (option is null)
            {
                return NotFound();
            }

            _context.QuestionOptions.Remove(option);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("exams")]
        public async Task<ActionResult<IReadOnlyList<ExamDto>>> GetExams(CancellationToken cancellationToken)
        {
            var items = await _context.Exams.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(200)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("exams/{id:int}")]
        public async Task<ActionResult<ExamDto>> GetExam(int id, CancellationToken cancellationToken)
        {
            var item = await _context.Exams.AsNoTracking()
                .Where(exam => exam.Id == id)
                .Select(exam => ToDto(exam))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("exams")]
        public async Task<IActionResult> CreateExam(ExamUpsertRequest request, CancellationToken cancellationToken)
        {
            var exam = new Exam
            {
                SubjectId = request.SubjectId,
                ClassId = request.ClassId,
                CreatedById = string.IsNullOrWhiteSpace(request.CreatedById) ? CurrentUserId : request.CreatedById.Trim(),
                Code = string.IsNullOrWhiteSpace(request.Code)
                    ? await _codeGenerator.GenerateExamCodeAsync(cancellationToken)
                    : request.Code.Trim().ToUpperInvariant(),
                Title = request.Title.Trim(),
                Instructions = NormalizeOptional(request.Instructions),
                DurationMinutes = request.DurationMinutes,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                PassingScore = request.PassingScore,
                MaxScore = request.MaxScore,
                ShuffleQuestions = request.ShuffleQuestions,
                ShuffleOptions = request.ShuffleOptions,
                RequireFullscreen = request.RequireFullscreen,
                MaxWarningCount = request.MaxWarningCount,
                ResultReleaseMode = request.ResultReleaseMode,
                Status = request.Status
            };

            _context.Exams.Add(exam);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetExam), new { id = exam.Id }, ToDto(exam));
        }

        [HttpPut("exams/{id:int}")]
        public async Task<IActionResult> UpdateExam(int id, ExamUpsertRequest request, CancellationToken cancellationToken)
        {
            var exam = await _context.Exams.FindAsync([id], cancellationToken);
            if (exam is null)
            {
                return NotFound();
            }

            exam.SubjectId = request.SubjectId;
            exam.ClassId = request.ClassId;
            exam.CreatedById = string.IsNullOrWhiteSpace(request.CreatedById) ? exam.CreatedById : request.CreatedById.Trim();
            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                exam.Code = request.Code.Trim().ToUpperInvariant();
            }

            exam.Title = request.Title.Trim();
            exam.Instructions = NormalizeOptional(request.Instructions);
            exam.DurationMinutes = request.DurationMinutes;
            exam.StartAt = request.StartAt;
            exam.EndAt = request.EndAt;
            exam.PassingScore = request.PassingScore;
            exam.MaxScore = request.MaxScore;
            exam.ShuffleQuestions = request.ShuffleQuestions;
            exam.ShuffleOptions = request.ShuffleOptions;
            exam.RequireFullscreen = request.RequireFullscreen;
            exam.MaxWarningCount = request.MaxWarningCount;
            exam.ResultReleaseMode = request.ResultReleaseMode;
            exam.Status = request.Status;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(exam));
        }

        [HttpDelete("exams/{id:int}")]
        public async Task<IActionResult> DeleteExam(int id, CancellationToken cancellationToken)
        {
            var exam = await _context.Exams.FindAsync([id], cancellationToken);
            if (exam is null)
            {
                return NotFound();
            }

            _context.Exams.Remove(exam);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("exam-questions")]
        public async Task<ActionResult<IReadOnlyList<ExamQuestionDto>>> GetExamQuestions(CancellationToken cancellationToken)
        {
            var items = await _context.ExamQuestions.AsNoTracking()
                .OrderBy(item => item.ExamId)
                .ThenBy(item => item.DisplayOrder)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpPost("exam-questions")]
        public async Task<IActionResult> CreateExamQuestion(ExamQuestionUpsertRequest request, CancellationToken cancellationToken)
        {
            var link = new ExamQuestion
            {
                ExamId = request.ExamId,
                QuestionId = request.QuestionId,
                Score = request.Score,
                DisplayOrder = request.DisplayOrder
            };

            _context.ExamQuestions.Add(link);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetExamQuestions), ToDto(link));
        }

        [HttpPut("exam-questions/{id:int}")]
        public async Task<IActionResult> UpdateExamQuestion(int id, ExamQuestionUpsertRequest request, CancellationToken cancellationToken)
        {
            var link = await _context.ExamQuestions.FindAsync([id], cancellationToken);
            if (link is null)
            {
                return NotFound();
            }

            link.ExamId = request.ExamId;
            link.QuestionId = request.QuestionId;
            link.Score = request.Score;
            link.DisplayOrder = request.DisplayOrder;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(link));
        }

        [HttpDelete("exam-questions/{id:int}")]
        public async Task<IActionResult> DeleteExamQuestion(int id, CancellationToken cancellationToken)
        {
            var link = await _context.ExamQuestions.FindAsync([id], cancellationToken);
            if (link is null)
            {
                return NotFound();
            }

            _context.ExamQuestions.Remove(link);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("exam-attempts")]
        public async Task<ActionResult<IReadOnlyList<ExamAttemptDto>>> GetExamAttempts(CancellationToken cancellationToken)
        {
            var items = await _context.ExamAttempts.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpGet("exam-attempts/{id:int}")]
        public async Task<ActionResult<ExamAttemptDto>> GetExamAttempt(int id, CancellationToken cancellationToken)
        {
            var item = await _context.ExamAttempts.AsNoTracking()
                .Where(attempt => attempt.Id == id)
                .Select(attempt => ToDto(attempt))
                .FirstOrDefaultAsync(cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost("exam-attempts")]
        public async Task<IActionResult> CreateExamAttempt(ExamAttemptUpsertRequest request, CancellationToken cancellationToken)
        {
            var attempt = new ExamAttempt
            {
                ExamId = request.ExamId,
                UserId = request.UserId.Trim(),
                SubmittedAt = request.SubmittedAt,
                Score = request.Score,
                Status = request.Status,
                IsAutoSubmitted = request.IsAutoSubmitted,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };

            _context.ExamAttempts.Add(attempt);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetExamAttempt), new { id = attempt.Id }, ToDto(attempt));
        }

        [HttpPut("exam-attempts/{id:int}")]
        public async Task<IActionResult> UpdateExamAttempt(int id, ExamAttemptUpsertRequest request, CancellationToken cancellationToken)
        {
            var attempt = await _context.ExamAttempts.FindAsync([id], cancellationToken);
            if (attempt is null)
            {
                return NotFound();
            }

            attempt.ExamId = request.ExamId;
            attempt.UserId = request.UserId.Trim();
            attempt.SubmittedAt = request.SubmittedAt;
            attempt.Score = request.Score;
            attempt.Status = request.Status;
            attempt.IsAutoSubmitted = request.IsAutoSubmitted;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(attempt));
        }

        [HttpDelete("exam-attempts/{id:int}")]
        public async Task<IActionResult> DeleteExamAttempt(int id, CancellationToken cancellationToken)
        {
            var attempt = await _context.ExamAttempts.FindAsync([id], cancellationToken);
            if (attempt is null)
            {
                return NotFound();
            }

            _context.ExamAttempts.Remove(attempt);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("attempt-answers")]
        public async Task<ActionResult<IReadOnlyList<AttemptAnswerDto>>> GetAttemptAnswers(CancellationToken cancellationToken)
        {
            var items = await _context.AttemptAnswers.AsNoTracking()
                .OrderBy(item => item.Id)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpPost("attempt-answers")]
        public async Task<IActionResult> CreateAttemptAnswer(AttemptAnswerUpsertRequest request, CancellationToken cancellationToken)
        {
            var answer = new AttemptAnswer
            {
                ExamAttemptId = request.ExamAttemptId,
                QuestionId = request.QuestionId,
                IsCorrect = request.IsCorrect,
                AwardedScore = request.AwardedScore
            };

            _context.AttemptAnswers.Add(answer);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetAttemptAnswers), ToDto(answer));
        }

        [HttpPut("attempt-answers/{id:int}")]
        public async Task<IActionResult> UpdateAttemptAnswer(int id, AttemptAnswerUpsertRequest request, CancellationToken cancellationToken)
        {
            var answer = await _context.AttemptAnswers.FindAsync([id], cancellationToken);
            if (answer is null)
            {
                return NotFound();
            }

            answer.ExamAttemptId = request.ExamAttemptId;
            answer.QuestionId = request.QuestionId;
            answer.IsCorrect = request.IsCorrect;
            answer.AwardedScore = request.AwardedScore;
            answer.LastSavedAt = DateTime.UtcNow;

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(answer));
        }

        [HttpDelete("attempt-answers/{id:int}")]
        public async Task<IActionResult> DeleteAttemptAnswer(int id, CancellationToken cancellationToken)
        {
            var answer = await _context.AttemptAnswers.FindAsync([id], cancellationToken);
            if (answer is null)
            {
                return NotFound();
            }

            _context.AttemptAnswers.Remove(answer);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        [HttpGet("anti-cheat-events")]
        public async Task<ActionResult<IReadOnlyList<AntiCheatEventDto>>> GetAntiCheatEvents(CancellationToken cancellationToken)
        {
            var items = await _context.AntiCheatEvents.AsNoTracking()
                .OrderByDescending(item => item.OccurredAt)
                .Take(300)
                .Select(item => ToDto(item))
                .ToListAsync(cancellationToken);

            return Ok(items);
        }

        [HttpPost("anti-cheat-events")]
        public async Task<IActionResult> CreateAntiCheatEvent(AntiCheatEventUpsertRequest request, CancellationToken cancellationToken)
        {
            var item = new AntiCheatEvent
            {
                ExamAttemptId = request.ExamAttemptId,
                EventType = request.EventType,
                Severity = request.Severity,
                Description = NormalizeOptional(request.Description),
                MetadataJson = NormalizeOptional(request.MetadataJson)
            };

            _context.AntiCheatEvents.Add(item);
            var result = await SaveChangesAsync(cancellationToken);
            return result ?? CreatedAtAction(nameof(GetAntiCheatEvents), ToDto(item));
        }

        [HttpPut("anti-cheat-events/{id:int}")]
        public async Task<IActionResult> UpdateAntiCheatEvent(int id, AntiCheatEventUpsertRequest request, CancellationToken cancellationToken)
        {
            var item = await _context.AntiCheatEvents.FindAsync([id], cancellationToken);
            if (item is null)
            {
                return NotFound();
            }

            item.ExamAttemptId = request.ExamAttemptId;
            item.EventType = request.EventType;
            item.Severity = request.Severity;
            item.Description = NormalizeOptional(request.Description);
            item.MetadataJson = NormalizeOptional(request.MetadataJson);

            var result = await SaveChangesAsync(cancellationToken);
            return result ?? Ok(ToDto(item));
        }

        [HttpDelete("anti-cheat-events/{id:int}")]
        public async Task<IActionResult> DeleteAntiCheatEvent(int id, CancellationToken cancellationToken)
        {
            var item = await _context.AntiCheatEvents.FindAsync([id], cancellationToken);
            if (item is null)
            {
                return NotFound();
            }

            _context.AntiCheatEvents.Remove(item);
            return await SaveChangesAsync(cancellationToken) ?? NoContent();
        }

        private async Task<DatabaseSummaryTable> CountAsync(string name, Func<Task<int>> count)
        {
            return new DatabaseSummaryTable
            {
                Name = name,
                Count = await count()
            };
        }

        private async Task<IActionResult?> SaveChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateException exception)
            {
                return Conflict(new
                {
                    message = "Database constraint failed. Check foreign keys, duplicate codes, or existing sample data.",
                    detail = exception.GetBaseException().Message
                });
            }
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static SubjectDto ToDto(Subject item) => new()
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description
        };

        private static ClassDto ToDto(ClassEntity item) => new()
        {
            Id = item.Id,
            SubjectId = item.SubjectId,
            TeacherId = item.TeacherId,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            Semester = item.Semester,
            AcademicYear = item.AcademicYear,
            IntroVideoUrl = item.IntroVideoUrl,
            CreatedAt = item.CreatedAt
        };

        private static ClassMemberDto ToDto(ClassMember item) => new()
        {
            ClassId = item.ClassId,
            UserId = item.UserId,
            JoinedAt = item.JoinedAt,
            Status = item.Status
        };

        private static QuestionDto ToDto(Question item) => new()
        {
            Id = item.Id,
            SubjectId = item.SubjectId,
            CreatedById = item.CreatedById,
            Content = item.Content,
            VideoUrl = item.VideoUrl,
            QuestionType = item.QuestionType,
            Difficulty = item.Difficulty,
            Explanation = item.Explanation,
            Status = item.Status,
            CreatedAt = item.CreatedAt
        };

        private static QuestionOptionDto ToDto(QuestionOption item) => new()
        {
            Id = item.Id,
            QuestionId = item.QuestionId,
            Content = item.Content,
            IsCorrect = item.IsCorrect,
            DisplayOrder = item.DisplayOrder
        };

        private static ExamDto ToDto(Exam item) => new()
        {
            Id = item.Id,
            SubjectId = item.SubjectId,
            ClassId = item.ClassId,
            CreatedById = item.CreatedById,
            Code = item.Code,
            Title = item.Title,
            DurationMinutes = item.DurationMinutes,
            StartAt = item.StartAt,
            EndAt = item.EndAt,
            PassingScore = item.PassingScore,
            MaxScore = item.MaxScore,
            Status = item.Status
        };

        private static ExamQuestionDto ToDto(ExamQuestion item) => new()
        {
            Id = item.Id,
            ExamId = item.ExamId,
            QuestionId = item.QuestionId,
            Score = item.Score,
            DisplayOrder = item.DisplayOrder
        };

        private static ExamAttemptDto ToDto(ExamAttempt item) => new()
        {
            Id = item.Id,
            ExamId = item.ExamId,
            UserId = item.UserId,
            StartedAt = item.StartedAt,
            SubmittedAt = item.SubmittedAt,
            Score = item.Score,
            Status = item.Status,
            IsAutoSubmitted = item.IsAutoSubmitted
        };

        private static AttemptAnswerDto ToDto(AttemptAnswer item) => new()
        {
            Id = item.Id,
            ExamAttemptId = item.ExamAttemptId,
            QuestionId = item.QuestionId,
            IsCorrect = item.IsCorrect,
            AwardedScore = item.AwardedScore,
            LastSavedAt = item.LastSavedAt
        };

        private static AntiCheatEventDto ToDto(AntiCheatEvent item) => new()
        {
            Id = item.Id,
            ExamAttemptId = item.ExamAttemptId,
            EventType = item.EventType,
            Severity = item.Severity,
            Description = item.Description,
            OccurredAt = item.OccurredAt
        };
    }
}
