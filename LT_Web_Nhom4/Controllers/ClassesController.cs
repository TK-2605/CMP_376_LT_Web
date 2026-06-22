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
        private readonly IAppClock _clock;

        public ClassesController(
            ApplicationDbContext context,
            IUniqueCodeGenerator codeGenerator,
            IAccessPolicy accessPolicy,
            IPrivateMediaStorage mediaStorage,
            IAppClock clock)
        {
            _context = context;
            _codeGenerator = codeGenerator;
            _accessPolicy = accessPolicy;
            _mediaStorage = mediaStorage;
            _clock = clock;
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
                AcademicYear = $"{_clock.Now.Year}-{_clock.Now.Year + 1}"
            };
            await PopulateSubjectsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateClassViewModel model, CancellationToken cancellationToken)
        {
            if (model.CreateNewSubject)
            {
                ModelState.Remove(nameof(model.SubjectId));
            }

            await PopulateSubjectsAsync(model);
            ValidateVideoUrl(model.IntroVideoUrl, nameof(model.IntroVideoUrl));
            var imageFiles = GetFiles(model.ImageFiles);
            if (model.CoverImage is not null && imageFiles.Count == 0)
            {
                imageFiles.Add(model.CoverImage);
            }

            var videoFiles = GetFiles(model.VideoFiles);
            ValidateMediaLimit(imageFiles.Count, videoFiles.Count, 0, 0, nameof(model.ImageFiles));
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var storedPaths = new List<string>();
            try
            {
                var subject = await ResolveSubjectAsync(model, cancellationToken);
                if (subject is null)
                {
                    return View(model);
                }

                var classRoom = new ClassEntity
                {
                    SubjectId = subject.Id,
                    TeacherId = CurrentUserId,
                    Code = await _codeGenerator.GenerateClassCodeAsync(cancellationToken),
                    Name = model.Name.Trim(),
                    Description = NormalizeOptional(model.Description),
                    Semester = NormalizeOptional(model.Semester),
                    AcademicYear = NormalizeOptional(model.AcademicYear),
                    IntroVideoUrl = NormalizeOptional(model.IntroVideoUrl)
                };

                if (subject.Id == 0)
                {
                    _context.Subjects.Add(subject);
                    classRoom.Subject = subject;
                }

                await AddClassMediaAsync(classRoom, imageFiles, videoFiles, storedPaths, cancellationToken);
                classRoom.CoverImagePath = classRoom.MediaAssets
                    .Where(media => media.MediaType == MediaAssetType.Image)
                    .OrderBy(media => media.DisplayOrder)
                    .Select(media => media.Path)
                    .FirstOrDefault();

                _context.Classes.Add(classRoom);
                await _context.SaveChangesAsync(cancellationToken);
                TempData["ClassMessage"] = "Lớp học đã được tạo. Mã tham gia được sinh tự động.";
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }
            catch (InvalidOperationException exception)
            {
                foreach (var path in storedPaths)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(nameof(model.ImageFiles), exception.Message);
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
                existingMember.JoinedAt = _clock.UtcNow;
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
                .Include(item => item.MediaAssets)
                .Include(item => item.Members).ThenInclude(member => member.User)
                .Include(item => item.Exams).ThenInclude(exam => exam.ExamQuestions)
                .Include(item => item.Exams).ThenInclude(exam => exam.Attempts)
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
                MediaAssets = ToClassMediaViewModels(classRoom).ToList(),
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                ExamCount = visibleExams.Count(),
                MemberCount = classRoom.Members.Count(member => member.Status == ClassMemberStatus.Active),
                Now = _clock.Now,
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
                Exams = visibleExams.OrderByDescending(exam => exam.StartAt).Select(exam =>
                {
                    var attempt = exam.Attempts
                        .Where(attempt => attempt.UserId == CurrentUserId)
                        .OrderByDescending(attempt => attempt.StartedAt)
                        .FirstOrDefault();
                    return new ExamCardViewModel
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
                        IsParticipant = !isOwner,
                        CurrentUserAttemptId = attempt?.Id,
                        CurrentUserSubmittedAt = attempt?.SubmittedAt,
                        CurrentUserAttemptStatus = attempt?.Status
                    };
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

            var classRoom = await _context.Classes.AsNoTracking()
                .Include(item => item.MediaAssets)
                .FirstOrDefaultAsync(item => item.Id == id);
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
                ExistingMedia = ToClassMediaViewModels(classRoom).ToList(),
                RowVersion = classRoom.RowVersion
            };
            await PopulateSubjectsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditClassViewModel model, CancellationToken cancellationToken)
        {
            if (model.CreateNewSubject)
            {
                ModelState.Remove(nameof(model.SubjectId));
            }

            if (!await _accessPolicy.IsClassOwnerAsync(model.Id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            await PopulateSubjectsAsync(model);
            ValidateVideoUrl(model.IntroVideoUrl, nameof(model.IntroVideoUrl));
            var classRoom = await _context.Classes
                .Include(item => item.MediaAssets)
                .FirstOrDefaultAsync(item => item.Id == model.Id);
            if (classRoom is null)
            {
                return NotFound();
            }

            model.Code = classRoom.Code;
            model.ExistingCoverImageUrl = classRoom.CoverImagePath is null ? null : Url.Action("ClassCover", "Media", new { id = model.Id });
            model.ExistingMedia = ToClassMediaViewModels(classRoom).ToList();
            var imageFiles = GetFiles(model.ImageFiles);
            if (model.CoverImage is not null && imageFiles.Count == 0)
            {
                imageFiles.Add(model.CoverImage);
            }

            var videoFiles = GetFiles(model.VideoFiles);
            var removeIds = model.RemoveMediaIds.Distinct().ToHashSet();
            var keptImageCount = classRoom.MediaAssets.Count(media => media.MediaType == MediaAssetType.Image && !removeIds.Contains(media.Id));
            var keptVideoCount = classRoom.MediaAssets.Count(media => media.MediaType == MediaAssetType.Video && !removeIds.Contains(media.Id));
            ValidateMediaLimit(imageFiles.Count, videoFiles.Count, keptImageCount, keptVideoCount, nameof(model.ImageFiles));
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var oldImagePath = classRoom.CoverImagePath;
            var storedPaths = new List<string>();
            var deletedPaths = new List<string>();
            try
            {
                var subject = await ResolveSubjectAsync(model, cancellationToken);
                if (subject is null)
                {
                    return View(model);
                }

                var removedMedia = classRoom.MediaAssets.Where(media => removeIds.Contains(media.Id)).ToList();
                foreach (var media in removedMedia)
                {
                    deletedPaths.Add(media.Path);
                    classRoom.MediaAssets.Remove(media);
                    _context.ClassMedia.Remove(media);
                }

                await AddClassMediaAsync(classRoom, imageFiles, videoFiles, storedPaths, cancellationToken);
                classRoom.SubjectId = subject.Id;
                if (subject.Id == 0)
                {
                    _context.Subjects.Add(subject);
                    classRoom.Subject = subject;
                }

                classRoom.Name = model.Name.Trim();
                classRoom.Description = NormalizeOptional(model.Description);
                classRoom.Semester = NormalizeOptional(model.Semester);
                classRoom.AcademicYear = NormalizeOptional(model.AcademicYear);
                classRoom.IntroVideoUrl = NormalizeOptional(model.IntroVideoUrl);
                classRoom.CoverImagePath = classRoom.MediaAssets
                    .Where(media => media.MediaType == MediaAssetType.Image)
                    .OrderBy(media => media.DisplayOrder)
                    .Select(media => media.Path)
                    .FirstOrDefault();
                if (classRoom.CoverImagePath is null && !model.RemoveCoverImage)
                {
                    classRoom.CoverImagePath = oldImagePath;
                }

                if (model.RemoveCoverImage && oldImagePath is not null && classRoom.MediaAssets.All(media => media.Path != oldImagePath))
                {
                    deletedPaths.Add(oldImagePath);
                    if (classRoom.CoverImagePath == oldImagePath)
                    {
                        classRoom.CoverImagePath = null;
                    }
                }

                _context.Entry(classRoom).Property(item => item.RowVersion).OriginalValue = model.RowVersion;
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var path in deletedPaths.Distinct())
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                TempData["ClassMessage"] = "Thông tin lớp đã được cập nhật.";
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var path in storedPaths)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(string.Empty, "Lớp vừa được cập nhật ở nơi khác. Vui lòng tải lại trang.");
                model.ExistingMedia = ToClassMediaViewModels(classRoom).ToList();
                return View(model);
            }
            catch (InvalidOperationException exception)
            {
                foreach (var path in storedPaths)
                {
                    await _mediaStorage.DeleteAsync(path, cancellationToken);
                }

                ModelState.AddModelError(nameof(model.ImageFiles), exception.Message);
                model.ExistingMedia = ToClassMediaViewModels(classRoom).ToList();
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
            if (member is null)
            {
                if (IsAjaxRequest())
                {
                    return NotFound(new { ok = false, message = "Không tìm thấy học viên trong lớp." });
                }

                return RedirectToAction(nameof(Details), new { id });
            }

            member.Status = ClassMemberStatus.Removed;
            await _context.SaveChangesAsync();

            var memberCount = await _context.ClassMembers
                .CountAsync(item => item.ClassId == id && item.Status == ClassMemberStatus.Active);
            var message = "Học viên đã được xóa khỏi lớp.";

            if (IsAjaxRequest())
            {
                return Json(new { ok = true, message, memberCount });
            }

            TempData["ClassMessage"] = message;
            member = null;
            if (member is not null)
            {
                member.Status = ClassMemberStatus.Removed;
                await _context.SaveChangesAsync();
                TempData["ClassMessage"] = "Học viên đã được xoá khỏi lớp.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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

        private async Task AddClassMediaAsync(
            ClassEntity classRoom,
            IReadOnlyList<IFormFile> imageFiles,
            IReadOnlyList<IFormFile> videoFiles,
            IList<string> storedPaths,
            CancellationToken cancellationToken)
        {
            var nextImageOrder = NextMediaOrder(classRoom.MediaAssets, MediaAssetType.Image);
            foreach (var file in imageFiles)
            {
                var path = await _mediaStorage.SaveImageAsync(file, "classes", cancellationToken);
                storedPaths.Add(path);
                classRoom.MediaAssets.Add(new ClassMedia
                {
                    MediaType = MediaAssetType.Image,
                    Path = path,
                    OriginalFileName = file.FileName,
                    DisplayOrder = nextImageOrder++
                });
            }

            var nextVideoOrder = NextMediaOrder(classRoom.MediaAssets, MediaAssetType.Video);
            foreach (var file in videoFiles)
            {
                var path = await _mediaStorage.SaveVideoAsync(file, "classes", cancellationToken);
                storedPaths.Add(path);
                classRoom.MediaAssets.Add(new ClassMedia
                {
                    MediaType = MediaAssetType.Video,
                    Path = path,
                    OriginalFileName = file.FileName,
                    DisplayOrder = nextVideoOrder++
                });
            }
        }

        private IEnumerable<MediaAssetViewModel> ToClassMediaViewModels(ClassEntity classRoom)
        {
            return classRoom.MediaAssets
                .OrderBy(media => media.MediaType)
                .ThenBy(media => media.DisplayOrder)
                .Select(media => new MediaAssetViewModel
                {
                    Id = media.Id,
                    MediaType = media.MediaType,
                    FileName = media.OriginalFileName,
                    Url = Url.Action("ClassMedia", "Media", new { id = classRoom.Id, mediaId = media.Id }) ?? string.Empty
                });
        }

        private static List<IFormFile> GetFiles(IFormFileCollection? files)
        {
            return files?.Where(file => file is not null && file.Length > 0).ToList() ?? new List<IFormFile>();
        }

        private void ValidateMediaLimit(int newImageCount, int newVideoCount, int existingImageCount, int existingVideoCount, string key)
        {
            if (existingImageCount + newImageCount > 5)
            {
                ModelState.AddModelError(key, "Mỗi lớp chỉ được lưu tối đa 5 ảnh.");
            }

            if (existingVideoCount + newVideoCount > 2)
            {
                ModelState.AddModelError(key, "Mỗi lớp chỉ được lưu tối đa 2 video.");
            }
        }

        private static int NextMediaOrder(IEnumerable<ClassMedia> mediaAssets, MediaAssetType mediaType)
        {
            var ordered = mediaAssets.Where(media => media.MediaType == mediaType).ToList();
            return ordered.Count == 0 ? 1 : ordered.Max(media => media.DisplayOrder) + 1;
        }

        private async Task PopulateSubjectsAsync(CreateClassViewModel model)
        {
            model.Subjects = await _context.Subjects.AsNoTracking()
                .Where(item => item.OwnerId == null || item.OwnerId == CurrentUserId || IsAdmin)
                .OrderBy(item => item.Name)
                .Select(item => new SubjectOptionViewModel { Id = item.Id, Label = item.Code + " - " + item.Name })
                .ToListAsync();
            if (model.SubjectId == 0 && model.Subjects.Count > 0)
            {
                model.SubjectId = model.Subjects[0].Id;
            }
        }

        private async Task<Subject?> ResolveSubjectAsync(CreateClassViewModel model, CancellationToken cancellationToken)
        {
            if (!model.CreateNewSubject)
            {
                var subjectExists = await _context.Subjects.AnyAsync(
                    item => item.Id == model.SubjectId
                        && (item.OwnerId == null || item.OwnerId == CurrentUserId || IsAdmin),
                    cancellationToken);
                if (!subjectExists)
                {
                    ModelState.AddModelError(nameof(model.SubjectId), "Vui lòng chọn môn học hợp lệ hoặc tạo môn học riêng.");
                    return null;
                }

                return new Subject { Id = model.SubjectId };
            }

            var code = NormalizeSubjectCode(model.NewSubjectCode);
            var name = NormalizeOptional(model.NewSubjectName);
            if (string.IsNullOrWhiteSpace(code))
            {
                ModelState.AddModelError(nameof(model.NewSubjectCode), "Vui lòng nhập mã môn học.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError(nameof(model.NewSubjectName), "Vui lòng nhập tên môn học.");
            }

            if (!ModelState.IsValid)
            {
                return null;
            }

            var duplicateCode = await _context.Subjects.AnyAsync(
                item => item.Code.ToUpper() == code,
                cancellationToken);
            if (duplicateCode)
            {
                ModelState.AddModelError(nameof(model.NewSubjectCode), "Mã môn học đã tồn tại. Vui lòng dùng mã khác.");
            }

            var normalizedName = name!.ToUpperInvariant();
            var duplicateName = await _context.Subjects.AnyAsync(
                item => item.Name.ToUpper() == normalizedName
                    && (item.OwnerId == null || item.OwnerId == CurrentUserId || IsAdmin),
                cancellationToken);
            if (duplicateName)
            {
                ModelState.AddModelError(nameof(model.NewSubjectName), "Tên môn học đã tồn tại trong danh sách của bạn.");
            }

            if (!ModelState.IsValid)
            {
                return null;
            }

            return new Subject
            {
                Code = code,
                Name = name,
                Description = NormalizeOptional(model.NewSubjectDescription),
                OwnerId = CurrentUserId
            };
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

        private static string NormalizeSubjectCode(string? value)
        {
            var raw = new string((value ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
                .ToArray());
            return raw.Length > 50 ? raw[..50] : raw;
        }
    }
}
