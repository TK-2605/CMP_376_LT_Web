using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ClassesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUniqueCodeGenerator _codeGenerator;
        private readonly IAccessPolicy _accessPolicy;
        private readonly IPrivateMediaStorage _mediaStorage;

        public ClassesController(
            ApplicationDbContext context,
            IUniqueCodeGenerator codeGenerator,
            IAccessPolicy accessPolicy,
            IPrivateMediaStorage mediaStorage)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _accessPolicy = accessPolicy;
            _mediaStorage = mediaStorage;
        }

        public async Task<IActionResult> Index()
        {
            return View(await BuildListModelAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateClassViewModel
            {
                AcademicYear = $"{DateTime.Now.Year}-{DateTime.Now.Year + 1}"
            };
            await PopulateSubjectsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateClassViewModel model, CancellationToken cancellationToken)
        {
            await PopulateSubjectsAsync(model);
            ValidateVideoUrl(model.IntroVideoUrl, nameof(model.IntroVideoUrl));
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string? coverImagePath = null;
            try
            {
                if (model.CoverImage is not null)
                {
                    coverImagePath = await _mediaStorage.SaveImageAsync(model.CoverImage, "classes", cancellationToken);
                }

                var classRoom = new ClassEntity
                {
                    SubjectId = model.SubjectId,
                    TeacherId = CurrentUserId,
                    Code = await _codeGenerator.GenerateClassCodeAsync(cancellationToken),
                    Name = model.Name.Trim(),
                    Description = NormalizeOptional(model.Description),
                    Semester = NormalizeOptional(model.Semester),
                    AcademicYear = NormalizeOptional(model.AcademicYear),
                    IntroVideoUrl = NormalizeOptional(model.IntroVideoUrl),
                    CoverImagePath = coverImagePath
                };

                _context.Classes.Add(classRoom);
                await _context.SaveChangesAsync(cancellationToken);
                TempData["ClassMessage"] = "Lớp học đã được tạo. Mã tham gia được sinh tự động.";
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }
            catch (InvalidOperationException exception)
            {
                await _mediaStorage.DeleteAsync(coverImagePath, cancellationToken);
                ModelState.AddModelError(nameof(model.CoverImage), exception.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(JoinClassViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var listModel = await BuildListModelAsync();
                listModel.JoinClass = model;
                return View(nameof(Index), listModel);
            }

            var code = model.Code.Trim().ToUpperInvariant();
            var classRoom = await _context.Classes.FirstOrDefaultAsync(item => item.Code == code);
            if (classRoom is null)
            {
                ModelState.AddModelError(nameof(model.Code), "Không tìm thấy lớp với mã này.");
                var listModel = await BuildListModelAsync();
                listModel.JoinClass = model;
                return View(nameof(Index), listModel);
            }

            if (classRoom.TeacherId == CurrentUserId)
            {
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }

            var existingMember = await _context.ClassMembers.FindAsync(classRoom.Id, CurrentUserId);
            if (existingMember is null)
            {
                _context.ClassMembers.Add(new ClassMember
                {
                    ClassId = classRoom.Id,
                    UserId = CurrentUserId,
                    Status = ClassMemberStatus.Active
                });
            }
            else
            {
                existingMember.Status = ClassMemberStatus.Active;
                existingMember.JoinedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["ClassMessage"] = "Bạn đã tham gia lớp thành công.";
            return RedirectToAction(nameof(Details), new { id = classRoom.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!await _accessPolicy.CanAccessClassAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var classRoom = await _context.Classes
                .AsNoTracking()
                .Include(item => item.Subject)
                .Include(item => item.Members).ThenInclude(member => member.User)
                .Include(item => item.Exams).ThenInclude(exam => exam.ExamQuestions)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (classRoom is null)
            {
                return NotFound();
            }

            var isOwner = IsAdmin || classRoom.TeacherId == CurrentUserId;
            var visibleExams = isOwner
                ? classRoom.Exams
                : classRoom.Exams.Where(exam => exam.Status != ExamStatus.Draft && exam.Status != ExamStatus.Cancelled);

            return View(new ClassDetailsViewModel
            {
                Id = classRoom.Id,
                Code = classRoom.Code,
                Name = classRoom.Name,
                SubjectName = classRoom.Subject.Name,
                Description = classRoom.Description,
                CoverImageUrl = classRoom.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = classRoom.Id }),
                IntroVideoUrl = classRoom.IntroVideoUrl,
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                ExamCount = visibleExams.Count(),
                MemberCount = classRoom.Members.Count(member => member.Status == ClassMemberStatus.Active),
                IsOwner = isOwner,
                Members = isOwner
                    ? classRoom.Members.Where(member => member.Status == ClassMemberStatus.Active).Select(member => new ClassMemberItemViewModel
                    {
                        UserId = member.UserId,
                        DisplayName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email ?? "Học viên" : member.User.FullName,
                        Email = member.User.Email ?? string.Empty,
                        Status = "Đang tham gia"
                    }).ToList()
                    : new List<ClassMemberItemViewModel>(),
                Exams = visibleExams.OrderByDescending(exam => exam.StartAt).Select(exam => new ExamCardViewModel
                {
                    Id = exam.Id,
                    Code = exam.Code,
                    Title = exam.Title,
                    SubjectName = classRoom.Subject.Name,
                    ClassName = classRoom.Name,
                    StartAt = exam.StartAt,
                    EndAt = exam.EndAt,
                    DurationMinutes = exam.DurationMinutes,
                    QuestionCount = exam.ExamQuestions.Count,
                    Status = exam.Status,
                    IsOwnedByCurrentUser = isOwner,
                    IsParticipant = !isOwner
                }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var classRoom = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
            if (classRoom is null)
            {
                return NotFound();
            }

            var model = new EditClassViewModel
            {
                Id = classRoom.Id,
                Code = classRoom.Code,
                SubjectId = classRoom.SubjectId,
                Name = classRoom.Name,
                Description = classRoom.Description,
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                IntroVideoUrl = classRoom.IntroVideoUrl,
                ExistingCoverImageUrl = classRoom.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id }),
                RowVersion = classRoom.RowVersion
            };
            await PopulateSubjectsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditClassViewModel model, CancellationToken cancellationToken)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(model.Id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            await PopulateSubjectsAsync(model);
            ValidateVideoUrl(model.IntroVideoUrl, nameof(model.IntroVideoUrl));
            var classRoom = await _context.Classes.FirstOrDefaultAsync(item => item.Id == model.Id);
            if (classRoom is null)
            {
                return NotFound();
            }

            model.Code = classRoom.Code;
            model.ExistingCoverImageUrl = classRoom.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = model.Id });
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var oldImagePath = classRoom.CoverImagePath;
            string? newImagePath = null;
            try
            {
                if (model.CoverImage is not null)
                {
                    newImagePath = await _mediaStorage.SaveImageAsync(model.CoverImage, "classes", cancellationToken);
                    classRoom.CoverImagePath = newImagePath;
                }
                else if (model.RemoveCoverImage)
                {
                    classRoom.CoverImagePath = null;
                }

                classRoom.SubjectId = model.SubjectId;
                classRoom.Name = model.Name.Trim();
                classRoom.Description = NormalizeOptional(model.Description);
                classRoom.Semester = NormalizeOptional(model.Semester);
                classRoom.AcademicYear = NormalizeOptional(model.AcademicYear);
                classRoom.IntroVideoUrl = NormalizeOptional(model.IntroVideoUrl);
                _context.Entry(classRoom).Property(item => item.RowVersion).OriginalValue = model.RowVersion;
                await _context.SaveChangesAsync(cancellationToken);

                if ((newImagePath is not null || model.RemoveCoverImage) && oldImagePath != classRoom.CoverImagePath)
                {
                    await _mediaStorage.DeleteAsync(oldImagePath, cancellationToken);
                }

                TempData["ClassMessage"] = "Thông tin lớp đã được cập nhật.";
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                await _mediaStorage.DeleteAsync(newImagePath, cancellationToken);
                ModelState.AddModelError(string.Empty, "Lớp vừa được cập nhật ở nơi khác. Vui lòng tải lại trang.");
                return View(model);
            }
            catch (InvalidOperationException exception)
            {
                await _mediaStorage.DeleteAsync(newImagePath, cancellationToken);
                ModelState.AddModelError(nameof(model.CoverImage), exception.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateCode(int id)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var classRoom = await _context.Classes.FirstOrDefaultAsync(item => item.Id == id);
            if (classRoom is null)
            {
                return NotFound();
            }

            classRoom.Code = await _codeGenerator.GenerateClassCodeAsync();
            await _context.SaveChangesAsync();
            TempData["ClassMessage"] = "Mã lớp mới đã được tạo. Mã cũ không còn hiệu lực.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int id, string userId)
        {
            if (!await _accessPolicy.IsClassOwnerAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var member = await _context.ClassMembers.FindAsync(id, userId);
            if (member is not null)
            {
                member.Status = ClassMemberStatus.Removed;
                await _context.SaveChangesAsync();
                TempData["ClassMessage"] = "Học viên đã được xoá khỏi lớp.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<ClassListViewModel> BuildListModelAsync()
        {
            var ownedClasses = await _context.Classes.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Exams).Include(item => item.Members)
                .Where(item => IsAdmin || item.TeacherId == CurrentUserId)
                .OrderByDescending(item => item.CreatedAt).ToListAsync();

            var participatingClasses = await _context.Classes.AsNoTracking()
                .Include(item => item.Subject).Include(item => item.Exams).Include(item => item.Members)
                .Where(item => item.Members.Any(member => member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active))
                .OrderByDescending(item => item.CreatedAt).ToListAsync();

            return new ClassListViewModel
            {
                OwnedClasses = ownedClasses.Select(ToCard).ToList(),
                ParticipatingClasses = participatingClasses.Where(item => item.TeacherId != CurrentUserId).Select(ToCard).ToList()
            };
        }

        private ClassCardViewModel ToCard(ClassEntity classRoom)
        {
            return new ClassCardViewModel
            {
                Id = classRoom.Id,
                Code = classRoom.Code,
                Name = classRoom.Name,
                SubjectName = classRoom.Subject.Name,
                Description = classRoom.Description,
                CoverImageUrl = classRoom.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = classRoom.Id }),
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                ExamCount = classRoom.Exams.Count,
                MemberCount = classRoom.Members.Count(member => member.Status == ClassMemberStatus.Active)
            };
        }

        private async Task PopulateSubjectsAsync(CreateClassViewModel model)
        {
            model.Subjects = await _context.Subjects.AsNoTracking().OrderBy(item => item.Name)
                .Select(item => new SubjectOptionViewModel { Id = item.Id, Label = item.Code + " - " + item.Name })
                .ToListAsync();
            if (model.SubjectId == 0 && model.Subjects.Count > 0)
            {
                model.SubjectId = model.Subjects[0].Id;
            }
        }

        private void ValidateVideoUrl(string? value, string key)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            {
                ModelState.AddModelError(key, "Video phải dùng liên kết HTTPS hợp lệ.");
            }
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private bool IsAdmin => User.IsInRole("Admin");

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }
    }
}
