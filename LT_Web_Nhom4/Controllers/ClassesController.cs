using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ClassesController : CrudController<ClassEntity>
    {
        public ClassesController(ApplicationDbContext context) : base(context)
        {
        }

        public override async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

            var ownedClasses = await Context.Classes
                .AsNoTracking()
                .Include(classRoom => classRoom.Subject)
                .Include(classRoom => classRoom.Exams)
                .Include(classRoom => classRoom.Members)
                .Where(classRoom => IsAdmin || classRoom.TeacherId == userId)
                .OrderByDescending(classRoom => classRoom.Id)
                .Take(100)
                .ToListAsync();

            var participatingClasses = await Context.ClassMembers
                .AsNoTracking()
                .Include(member => member.Class)
                    .ThenInclude(classRoom => classRoom.Subject)
                .Include(member => member.Class)
                    .ThenInclude(classRoom => classRoom.Exams)
                .Include(member => member.Class)
                    .ThenInclude(classRoom => classRoom.Members)
                .Where(member => member.UserId == userId
                    && member.Status == ClassMemberStatus.Active
                    && member.Class.TeacherId != userId)
                .Select(member => member.Class)
                .OrderByDescending(classRoom => classRoom.Id)
                .Take(100)
                .ToListAsync();

            return View(new ClassListViewModel
            {
                OwnedClasses = ownedClasses.Select(ToClassCard).ToList(),
                ParticipatingClasses = participatingClasses.Select(ToClassCard).ToList()
            });
        }

        public override IActionResult Create()
        {
            return View(new CreateClassViewModel
            {
                AcademicYear = DateTime.Now.Year.ToString()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> Create(IFormCollection form)
        {
            var model = new CreateClassViewModel
            {
                Code = form[nameof(CreateClassViewModel.Code)].ToString(),
                Name = form[nameof(CreateClassViewModel.Name)].ToString(),
                Semester = form[nameof(CreateClassViewModel.Semester)].ToString(),
                AcademicYear = form[nameof(CreateClassViewModel.AcademicYear)].ToString()
            };

            ModelState.Clear();
            TryValidateModel(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await Context.Classes.AnyAsync(classRoom => classRoom.Code == model.Code.Trim()))
            {
                ModelState.AddModelError(nameof(model.Code), "Mã lớp đã tồn tại.");
                return View(model);
            }

            var classRoom = new ClassEntity
            {
                SubjectId = await EnsureDefaultSubjectAsync(),
                TeacherId = CurrentUserId ?? string.Empty,
                Code = model.Code.Trim(),
                Name = model.Name.Trim(),
                Semester = model.Semester?.Trim(),
                AcademicYear = model.AcademicYear?.Trim()
            };

            if (!await CanCreateAsync(classRoom))
            {
                return Forbid();
            }

            Context.Classes.Add(classRoom);
            await Context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = classRoom.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(JoinClassViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return await Index();
            }

            var code = model.Code.Trim();
            var classRoom = await Context.Classes.FirstOrDefaultAsync(item => item.Code == code);
            if (classRoom is null)
            {
                TempData["ClassMessage"] = "Không tìm thấy lớp theo mã đã nhập.";
                return RedirectToAction(nameof(Index));
            }

            if (classRoom.TeacherId == CurrentUserId)
            {
                TempData["ClassMessage"] = "Bạn đang là người tạo lớp này.";
                return RedirectToAction(nameof(Details), new { id = classRoom.Id });
            }

            var existingMember = await Context.ClassMembers.FindAsync(classRoom.Id, CurrentUserId);
            if (existingMember is null)
            {
                Context.ClassMembers.Add(new ClassMember
                {
                    ClassId = classRoom.Id,
                    UserId = CurrentUserId ?? string.Empty,
                    Status = ClassMemberStatus.Active
                });
            }
            else
            {
                existingMember.Status = ClassMemberStatus.Active;
            }

            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = classRoom.Id });
        }

        public override async Task<IActionResult> Details(string id)
        {
            if (!int.TryParse(id, out var classId))
            {
                return NotFound();
            }

            var classRoom = await Context.Classes
                .AsNoTracking()
                .Include(item => item.Subject)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Exams)
                    .ThenInclude(exam => exam.Subject)
                .Include(item => item.Exams)
                    .ThenInclude(exam => exam.ExamQuestions)
                .FirstOrDefaultAsync(item => item.Id == classId);

            if (classRoom is null)
            {
                return NotFound();
            }

            var isOwner = IsAdmin || classRoom.TeacherId == CurrentUserId;
            var isMember = classRoom.Members.Any(member =>
                member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active);

            if (!isOwner && !isMember)
            {
                return Forbid();
            }

            return View(new ClassDetailsViewModel
            {
                Id = classRoom.Id,
                Code = classRoom.Code,
                Name = classRoom.Name,
                SubjectName = classRoom.Subject?.Name ?? "Môn học chung",
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                ExamCount = classRoom.Exams.Count,
                MemberCount = classRoom.Members.Count(member => member.Status == ClassMemberStatus.Active),
                IsOwner = isOwner,
                Members = classRoom.Members
                    .Where(member => member.Status == ClassMemberStatus.Active)
                    .Select(member => new ClassMemberItemViewModel
                    {
                        DisplayName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email ?? "Học viên" : member.User.FullName,
                        Email = member.User.Email ?? string.Empty,
                        Status = ToVietnameseStatus(member.Status)
                    })
                    .ToList(),
                Exams = classRoom.Exams
                    .OrderByDescending(exam => exam.StartAt)
                    .Select(exam => new ExamCardViewModel
                    {
                        Id = exam.Id,
                        Title = exam.Title,
                        SubjectName = exam.Subject?.Name ?? classRoom.Subject?.Name ?? "Môn học chung",
                        ClassName = classRoom.Name,
                        StartAt = exam.StartAt,
                        EndAt = exam.EndAt,
                        DurationMinutes = exam.DurationMinutes,
                        QuestionCount = exam.ExamQuestions.Count,
                        IsOwnedByCurrentUser = isOwner,
                        IsParticipant = !isOwner
                    })
                    .ToList()
            });
        }

        protected override IQueryable<ClassEntity> ApplyReadScope(IQueryable<ClassEntity> query)
        {
            return IsAdmin ? query : query.Where(classRoom => classRoom.TeacherId == CurrentUserId);
        }

        protected override Task<bool> CanReadAsync(ClassEntity entity)
        {
            return Task.FromResult(IsAdmin || entity.TeacherId == CurrentUserId);
        }

        protected override Task<bool> CanCreateAsync(ClassEntity entity)
        {
            return Task.FromResult(User.Identity?.IsAuthenticated == true);
        }

        protected override Task<bool> CanUpdateAsync(ClassEntity entity)
        {
            return CanReadAsync(entity);
        }

        protected override Task<bool> CanDeleteAsync(ClassEntity entity)
        {
            return CanReadAsync(entity);
        }

        protected override async Task OnCreatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            if (entity.SubjectId <= 0)
            {
                entity.SubjectId = await EnsureDefaultSubjectAsync();
            }
        }

        protected override async Task OnUpdatingAsync(ClassEntity entity)
        {
            if (!IsAdmin && !string.IsNullOrWhiteSpace(CurrentUserId))
            {
                entity.TeacherId = CurrentUserId;
            }

            if (entity.SubjectId <= 0)
            {
                entity.SubjectId = await EnsureDefaultSubjectAsync();
            }
        }

        private async Task<int> EnsureDefaultSubjectAsync()
        {
            var subject = await Context.Subjects.FirstOrDefaultAsync(item => item.Code == "GENERAL");
            if (subject is not null)
            {
                return subject.Id;
            }

            subject = new LT_Web_Nhom4.Models.Subject
            {
                Code = "GENERAL",
                Name = "Mon hoc chung",
                Description = "Mon hoc mac dinh de tao lop/phong thi nhanh."
            };

            Context.Subjects.Add(subject);
            await Context.SaveChangesAsync();
            return subject.Id;
        }

        private static ClassCardViewModel ToClassCard(ClassEntity classRoom)
        {
            return new ClassCardViewModel
            {
                Id = classRoom.Id,
                Code = classRoom.Code,
                Name = classRoom.Name,
                SubjectName = classRoom.Subject?.Name ?? "Môn học chung",
                Semester = classRoom.Semester,
                AcademicYear = classRoom.AcademicYear,
                ExamCount = classRoom.Exams.Count,
                MemberCount = classRoom.Members.Count(member => member.Status == ClassMemberStatus.Active)
            };
        }

        private static string ToVietnameseStatus(ClassMemberStatus status)
        {
            return status switch
            {
                ClassMemberStatus.Active => "Đang tham gia",
                ClassMemberStatus.Pending => "Chờ duyệt",
                ClassMemberStatus.Removed => "Đã xoá",
                _ => status.ToString()
            };
        }
    }
}
