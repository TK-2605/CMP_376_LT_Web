using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LT_Web_Nhom4.Controllers
{
    public abstract class CrudController<TEntity> : Controller where TEntity : class, new()
    {
        private const string Separator = "|";
        private static readonly Dictionary<string, string> EntityTitles = new()
        {
            ["Subject"] = "Môn học",
            ["Class"] = "Lớp học",
            ["ClassMember"] = "Thành viên lớp",
            ["Question"] = "Câu hỏi",
            ["QuestionOption"] = "Lựa chọn đáp án",
            ["Exam"] = "Đề thi",
            ["ExamQuestion"] = "Câu hỏi trong đề",
            ["ExamAttempt"] = "Lượt làm bài",
            ["AttemptAnswer"] = "Đáp án bài làm",
            ["AntiCheatEvent"] = "Cảnh báo bài thi"
        };

        private static readonly Dictionary<string, string> EntityDescriptions = new()
        {
            ["Subject"] = "Quản lý các môn học dùng để phân loại lớp, câu hỏi và đề thi.",
            ["Class"] = "Quản lý lớp học và người phụ trách.",
            ["ClassMember"] = "Quản lý học viên trong từng lớp học.",
            ["Question"] = "Quản lý ngân hàng câu hỏi và trạng thái sử dụng.",
            ["QuestionOption"] = "Quản lý các lựa chọn của từng câu hỏi.",
            ["Exam"] = "Quản lý thông tin đề thi, thời gian và cấu hình làm bài.",
            ["ExamQuestion"] = "Gắn câu hỏi vào đề thi và thiết lập điểm.",
            ["ExamAttempt"] = "Theo dõi các lượt làm bài của học viên.",
            ["AttemptAnswer"] = "Xem đáp án đã lưu và điểm từng câu.",
            ["AntiCheatEvent"] = "Theo dõi các cảnh báo cần xem lại."
        };

        private static readonly Dictionary<string, string[]> ListFields = new()
        {
            ["Subject"] = new[] { "Code", "Name", "Description" },
            ["Class"] = new[] { "Code", "Name", "Semester", "AcademicYear", "CreatedAt" },
            ["ClassMember"] = new[] { "ClassId", "UserId", "Status", "JoinedAt" },
            ["Question"] = new[] { "Content", "QuestionType", "Difficulty", "Status" },
            ["QuestionOption"] = new[] { "QuestionId", "Content", "IsCorrect", "DisplayOrder" },
            ["Exam"] = new[] { "Code", "Title", "DurationMinutes", "MaxScore", "StartAt", "EndAt", "Status" },
            ["ExamQuestion"] = new[] { "ExamId", "QuestionId", "Score", "DisplayOrder" },
            ["ExamAttempt"] = new[] { "ExamId", "UserId", "StartedAt", "SubmittedAt", "Score", "Status" },
            ["AttemptAnswer"] = new[] { "ExamAttemptId", "QuestionId", "IsCorrect", "AwardedScore", "LastSavedAt" },
            ["AntiCheatEvent"] = new[] { "ExamAttemptId", "EventType", "Severity", "OccurredAt" }
        };

        private static readonly Dictionary<string, string[]> FormFields = new()
        {
            ["Subject"] = new[] { "Code", "Name", "Description" },
            ["Class"] = new[] { "SubjectId", "TeacherId", "Name", "Description", "Semester", "AcademicYear", "IntroVideoUrl" },
            ["ClassMember"] = new[] { "ClassId", "UserId", "Status" },
            ["Question"] = new[] { "SubjectId", "CreatedById", "Content", "VideoUrl", "QuestionType", "Difficulty", "Explanation", "Status" },
            ["QuestionOption"] = new[] { "QuestionId", "Content", "IsCorrect", "DisplayOrder" },
            ["Exam"] = new[] { "SubjectId", "ClassId", "CreatedById", "Title", "Instructions", "DurationMinutes", "StartAt", "EndAt", "MaxScore", "PassingScore", "ShuffleQuestions", "ShuffleOptions", "RequireFullscreen", "MaxWarningCount", "ResultReleaseMode", "Status" },
            ["ExamQuestion"] = new[] { "ExamId", "QuestionId", "Score", "DisplayOrder" },
            ["ExamAttempt"] = new[] { "ExamId", "UserId", "SubmittedAt", "Score", "Status", "IsAutoSubmitted" },
            ["AttemptAnswer"] = new[] { "ExamAttemptId", "QuestionId", "IsCorrect", "AwardedScore" },
            ["AntiCheatEvent"] = new[] { "ExamAttemptId", "EventType", "Severity", "Description", "OccurredAt" }
        };

        private static readonly Dictionary<string, string> FieldLabels = new()
        {
            ["AcademicYear"] = "Năm học",
            ["AwardedScore"] = "Điểm đạt được",
            ["ClassId"] = "Lớp học",
            ["Code"] = "Mã",
            ["Content"] = "Nội dung",
            ["CreatedAt"] = "Ngày tạo",
            ["CreatedById"] = "Người tạo",
            ["Description"] = "Mô tả",
            ["Difficulty"] = "Độ khó",
            ["DisplayOrder"] = "Thứ tự hiển thị",
            ["DurationMinutes"] = "Thời lượng",
            ["EndAt"] = "Kết thúc",
            ["EventType"] = "Loại sự kiện",
            ["ExamAttemptId"] = "Lượt làm bài",
            ["ExamId"] = "Đề thi",
            ["Explanation"] = "Giải thích",
            ["Instructions"] = "Hướng dẫn làm bài",
            ["IntroVideoUrl"] = "Video giới thiệu",
            ["IsAutoSubmitted"] = "Tự động nộp bài",
            ["IsCorrect"] = "Đáp án đúng",
            ["MaxWarningCount"] = "Giới hạn cảnh báo",
            ["MaxScore"] = "Điểm tối đa",
            ["Name"] = "Tên",
            ["OccurredAt"] = "Thời điểm",
            ["PassingScore"] = "Điểm đạt",
            ["QuestionId"] = "Câu hỏi",
            ["QuestionType"] = "Loại câu hỏi",
            ["RequireFullscreen"] = "Yêu cầu toàn màn hình",
            ["ResultReleaseMode"] = "Công bố kết quả",
            ["Score"] = "Điểm",
            ["Semester"] = "Học kỳ",
            ["Severity"] = "Mức độ",
            ["ShuffleOptions"] = "Trộn đáp án",
            ["ShuffleQuestions"] = "Trộn câu hỏi",
            ["StartAt"] = "Bắt đầu",
            ["StartedAt"] = "Bắt đầu làm",
            ["Status"] = "Trạng thái",
            ["SubjectId"] = "Môn học",
            ["SubmittedAt"] = "Thời gian nộp",
            ["TeacherId"] = "Chủ lớp",
            ["Title"] = "Tiêu đề",
            ["UserId"] = "Người dùng",
            ["VideoUrl"] = "Liên kết video"
        };

        protected readonly ApplicationDbContext Context;
        private readonly IEntityType _entityType;

        protected CrudController(ApplicationDbContext context)
        {
            Context = context;
            _entityType = context.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} is not part of ApplicationDbContext.");
        }

        public virtual async Task<IActionResult> Index()
        {
            var entities = await ApplyReadScope(Context.Set<TEntity>().AsNoTracking()).Take(200).ToListAsync();
            var fields = BuildFields();
            var rows = entities.Select(entity => new CrudRowViewModel
            {
                Key = BuildKey(entity),
                Values = fields.ToDictionary(
                    field => field.Name,
                    field => FormatValue(GetProperty(field.Name).GetValue(entity)))
            }).ToList();

            return SharedCrudView("Index", new CrudIndexViewModel
            {
                Title = GetTitle(),
                ControllerName = ControllerName,
                AreaName = AreaName,
                Description = GetDescription(),
                Fields = fields,
                Rows = rows
            });
        }

        public virtual async Task<IActionResult> Details(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
            }
            if (!await CanReadAsync(entity))
            {
                return Forbid();
            }

            return SharedCrudView("Details", new CrudDetailsViewModel
            {
                Title = GetTitle(),
                ControllerName = ControllerName,
                AreaName = AreaName,
                Description = GetDescription(),
                Key = id,
                Fields = BuildFields(entity, readOnlyKeys: true)
            });
        }

        public virtual IActionResult Create()
        {
            return SharedCrudView("Create", new CrudFormViewModel
            {
                Title = $"Them {GetTitle()}",
                Description = GetDescription(),
                ActionName = nameof(Create),
                ControllerName = ControllerName,
                AreaName = AreaName,
                Fields = BuildFields(new TEntity(), readOnlyKeys: false)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Create(IFormCollection form)
        {
            var entity = new TEntity();
            ApplyForm(entity, form, readOnlyKeys: false);
            await OnCreatingAsync(entity);

            if (!await CanCreateAsync(entity))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return SharedCrudView("Create", new CrudFormViewModel
                {
                    Title = $"Them {GetTitle()}",
                    Description = GetDescription(),
                    ActionName = nameof(Create),
                    ControllerName = ControllerName,
                    AreaName = AreaName,
                    Fields = BuildFields(entity, readOnlyKeys: false)
                });
            }

            Context.Add(entity);
            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        public virtual async Task<IActionResult> Edit(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
            }
            if (!await CanUpdateAsync(entity))
            {
                return Forbid();
            }

            return SharedCrudView("Edit", new CrudFormViewModel
            {
                Title = $"Sua {GetTitle()}",
                Description = GetDescription(),
                ActionName = nameof(Edit),
                ControllerName = ControllerName,
                AreaName = AreaName,
                Key = id,
                Fields = BuildFields(entity, readOnlyKeys: true)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> Edit(string id, IFormCollection form)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
            }
            if (!await CanUpdateAsync(entity))
            {
                return Forbid();
            }

            ApplyForm(entity, form, readOnlyKeys: true);
            await OnUpdatingAsync(entity);

            if (!ModelState.IsValid)
            {
                return SharedCrudView("Edit", new CrudFormViewModel
                {
                    Title = $"Sua {GetTitle()}",
                    Description = GetDescription(),
                    ActionName = nameof(Edit),
                    ControllerName = ControllerName,
                    AreaName = AreaName,
                    Key = id,
                    Fields = BuildFields(entity, readOnlyKeys: true)
                });
            }

            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        public virtual async Task<IActionResult> Delete(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
            }
            if (!await CanDeleteAsync(entity))
            {
                return Forbid();
            }

            return SharedCrudView("Delete", new CrudDeleteViewModel
            {
                Title = GetTitle(),
                ControllerName = ControllerName,
                AreaName = AreaName,
                Description = GetDescription(),
                Key = id,
                Fields = BuildFields(entity, readOnlyKeys: true)
            });
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public virtual async Task<IActionResult> DeleteConfirmed(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
            }
            if (!await CanDeleteAsync(entity))
            {
                return Forbid();
            }

            Context.Remove(entity);
            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        protected string ControllerName => ControllerContext.ActionDescriptor.ControllerName;

        protected string? AreaName => ControllerContext.ActionDescriptor.RouteValues.TryGetValue("area", out var area) ? area : null;

        protected string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        protected bool IsAdmin => User.IsInRole("Admin");

        protected virtual IQueryable<TEntity> ApplyReadScope(IQueryable<TEntity> query)
        {
            return query;
        }

        protected virtual Task<bool> CanReadAsync(TEntity entity)
        {
            return Task.FromResult(true);
        }

        protected virtual Task<bool> CanCreateAsync(TEntity entity)
        {
            return Task.FromResult(true);
        }

        protected virtual Task<bool> CanUpdateAsync(TEntity entity)
        {
            return Task.FromResult(true);
        }

        protected virtual Task<bool> CanDeleteAsync(TEntity entity)
        {
            return Task.FromResult(true);
        }

        protected virtual Task OnCreatingAsync(TEntity entity)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnUpdatingAsync(TEntity entity)
        {
            return Task.CompletedTask;
        }

        private static string GetTitle()
        {
            var entityName = typeof(TEntity).Name;
            return EntityTitles.TryGetValue(entityName, out var title) ? title : SplitPascalCase(entityName);
        }

        private static string GetDescription()
        {
            var entityName = typeof(TEntity).Name;
            return EntityDescriptions.TryGetValue(entityName, out var description) ? description : string.Empty;
        }

        private IActionResult SharedCrudView(string viewName, object model)
        {
            if (string.Equals(AreaName, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return View($"/Areas/Admin/Views/Shared/Crud/{viewName}.cshtml", model);
            }

            return View($"/Views/Shared/Crud/{viewName}.cshtml", model);
        }

        private async Task<TEntity?> FindAsync(string id)
        {
            var keyValues = ParseKey(id);
            return await Context.Set<TEntity>().FindAsync(keyValues.ToArray());
        }

        private IReadOnlyList<object?> ParseKey(string id)
        {
            var key = GetPrimaryKey();
            var rawValues = id.Split(Separator);
            if (rawValues.Length != key.Properties.Count)
            {
                throw new InvalidOperationException("Invalid entity key.");
            }

            return key.Properties
                .Select((property, index) => ConvertFromString(rawValues[index], property.ClrType))
                .ToList();
        }

        private string BuildKey(TEntity entity)
        {
            var key = GetPrimaryKey();
            return string.Join(Separator, key.Properties.Select(property =>
            {
                var value = GetProperty(property.Name).GetValue(entity);
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }));
        }

        private IKey GetPrimaryKey()
        {
            return _entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name} does not have a primary key.");
        }

        private IReadOnlyList<CrudFieldViewModel> BuildFields(TEntity? entity = null, bool readOnlyKeys = false)
        {
            var keyNames = GetPrimaryKey().Properties.Select(property => property.Name).ToHashSet();

            var entityName = typeof(TEntity).Name;
            var listFields = GetConfiguredFields(ListFields, entityName);
            var formFields = GetConfiguredFields(FormFields, entityName);
            var orderLookup = formFields
                .Concat(listFields)
                .Distinct()
                .Select((name, index) => new { name, index })
                .ToDictionary(item => item.name, item => item.index);

            return _entityType.GetProperties()
                .OrderBy(property => orderLookup.TryGetValue(property.Name, out var index) ? index : 999)
                .ThenBy(property => keyNames.Contains(property.Name) ? 0 : 1)
                .Select(property =>
                {
                    var clrProperty = GetProperty(property.Name);
                    var value = entity is null ? null : clrProperty.GetValue(entity);
                    var isKey = keyNames.Contains(property.Name);
                    var isGeneratedKey = IsGeneratedKey(property, isKey);
                    var showInList = listFields.Contains(property.Name);
                    var showInForm = (formFields.Contains(property.Name) || isKey && !readOnlyKeys) && !isGeneratedKey;
                    if (!IsAdmin && IsServerAssignedField(entityName, property.Name))
                    {
                        showInForm = false;
                    }

                    if (!IsAdmin && entityName == nameof(Exam) && property.Name == nameof(Exam.SubjectId))
                    {
                        showInForm = false;
                    }

                    return new CrudFieldViewModel
                    {
                        Name = property.Name,
                        Label = FieldLabels.TryGetValue(property.Name, out var label) ? label : SplitPascalCase(property.Name),
                        Value = FormatInputValue(value, property.ClrType),
                        InputType = GetInputType(property.ClrType),
                        IsKey = isKey,
                        IsReadOnly = readOnlyKeys && isKey,
                        IsNullable = property.IsNullable,
                        IsBoolean = GetUnderlyingType(property.ClrType) == typeof(bool),
                        ShowInList = showInList,
                        ShowInForm = showInForm,
                        ShowInDetails = showInList || showInForm,
                        Placeholder = BuildPlaceholder(property.Name),
                        HelpText = BuildHelpText(entityName, property.Name),
                        Options = BuildOptions(property.Name, property.ClrType)
                    };
                })
                .Where(field => field.ShowInList || field.ShowInForm || field.ShowInDetails)
                .ToList();
        }

        private void ApplyForm(TEntity entity, IFormCollection form, bool readOnlyKeys)
        {
            var keyNames = GetPrimaryKey().Properties.Select(property => property.Name).ToHashSet();

            var entityName = typeof(TEntity).Name;
            var formFields = GetConfiguredFields(FormFields, entityName);

            foreach (var property in _entityType.GetProperties())
            {
                var isKey = keyNames.Contains(property.Name);
                if (!readOnlyKeys && IsGeneratedKey(property, isKey))
                {
                    continue;
                }

                if (!formFields.Contains(property.Name) && !(keyNames.Contains(property.Name) && !readOnlyKeys))
                {
                    continue;
                }

                if (readOnlyKeys && isKey)
                {
                    continue;
                }

                var clrProperty = GetProperty(property.Name);
                if (!clrProperty.CanWrite)
                {
                    continue;
                }

                var rawValue = form[property.Name].FirstOrDefault();
                if (GetUnderlyingType(property.ClrType) == typeof(bool))
                {
                    rawValue = form[property.Name].Contains("true") ? "true" : "false";
                }

                try
                {
                    clrProperty.SetValue(entity, ConvertFromString(rawValue, property.ClrType));
                }
                catch (Exception)
                {
                    ModelState.AddModelError(property.Name, $"{SplitPascalCase(property.Name)} khong hop le.");
                }
            }
        }

        private PropertyInfo GetProperty(string name)
        {
            return typeof(TEntity).GetProperty(name)
                ?? throw new InvalidOperationException($"{typeof(TEntity).Name}.{name} was not found.");
        }

        private static object? ConvertFromString(string? rawValue, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (Nullable.GetUnderlyingType(targetType) is not null || targetType == typeof(string))
                {
                    return null;
                }

                return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
            }

            if (underlyingType.IsEnum)
            {
                return Enum.Parse(underlyingType, rawValue);
            }

            if (underlyingType == typeof(Guid))
            {
                return Guid.Parse(rawValue);
            }

            if (underlyingType == typeof(DateTime))
            {
                return DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(bool))
            {
                return rawValue == "true" || rawValue == "on";
            }

            return Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
        }

        private static string? FormatValue(object? value)
        {
            return value switch
            {
                null => null,
                DateTime dateTime => dateTime.ToString("dd/MM/yyyy HH:mm"),
                bool boolean => boolean ? "Có" : "Không",
                ExamStatus status => status.ToVietnamese(),
                ExamAttemptStatus status => status.ToVietnamese(),
                ResultReleaseMode mode => mode.ToVietnamese(),
                QuestionType type => type.ToVietnamese(),
                QuestionDifficulty difficulty => difficulty.ToVietnamese(),
                AntiCheatEventType eventType => eventType.ToVietnamese(),
                decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        private static string? FormatInputValue(object? value, Type type)
        {
            if (value is null)
            {
                return null;
            }

            var underlyingType = GetUnderlyingType(type);
            if (underlyingType == typeof(DateTime) && value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-ddTHH:mm");
            }

            if (underlyingType == typeof(bool) && value is bool boolean)
            {
                return boolean ? "true" : "false";
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string GetInputType(Type type)
        {
            var underlyingType = GetUnderlyingType(type);
            if (underlyingType == typeof(DateTime))
            {
                return "datetime-local";
            }

            if (underlyingType == typeof(int) || underlyingType == typeof(decimal) || underlyingType == typeof(double))
            {
                return "number";
            }

            return "text";
        }

        private IReadOnlyList<CrudOptionViewModel> BuildOptions(string propertyName, Type type)
        {
            var underlyingType = GetUnderlyingType(type);
            if (underlyingType.IsEnum)
            {
                return Enum.GetNames(underlyingType)
                    .Select(name => new CrudOptionViewModel { Value = name, Text = FormatEnumName(underlyingType, name) })
                    .ToList();
            }

            return propertyName switch
            {
                "SubjectId" => BuildSubjectOptions(),
                "TeacherId" => BuildUserOptions(),
                "ClassId" => BuildClassOptions(),
                "CreatedById" => BuildUserOptions(),
                "QuestionId" => BuildQuestionOptions(),
                "ExamId" => BuildExamOptions(),
                "UserId" => BuildUserOptions(),
                "ExamAttemptId" => BuildAttemptOptions(),
                _ => new List<CrudOptionViewModel>()
            };
        }

        private IReadOnlyList<CrudOptionViewModel> BuildSubjectOptions()
        {
            return Context.Subjects
                .AsNoTracking()
                .OrderBy(subject => subject.Name)
                .Take(200)
                .Select(subject => new CrudOptionViewModel
                {
                    Value = subject.Id.ToString(CultureInfo.InvariantCulture),
                    Text = subject.Code + " - " + subject.Name
                })
                .ToList();
        }

        private IReadOnlyList<CrudOptionViewModel> BuildClassOptions()
        {
            var query = Context.Classes.AsNoTracking();
            if (!IsAdmin)
            {
                query = query.Where(classRoom => classRoom.TeacherId == CurrentUserId);
            }

            return query
                .OrderBy(classRoom => classRoom.Name)
                .Take(200)
                .Select(classRoom => new CrudOptionViewModel
                {
                    Value = classRoom.Id.ToString(CultureInfo.InvariantCulture),
                    Text = classRoom.Code + " - " + classRoom.Name
                })
                .ToList();
        }

        private IReadOnlyList<CrudOptionViewModel> BuildQuestionOptions()
        {
            var query = Context.Questions.AsNoTracking();
            if (!IsAdmin)
            {
                query = query.Where(question => question.CreatedById == CurrentUserId);
            }

            return query
                .OrderByDescending(question => question.Id)
                .Take(200)
                .Select(question => new CrudOptionViewModel
                {
                    Value = question.Id.ToString(CultureInfo.InvariantCulture),
                    Text = question.Content.Length > 80 ? question.Content.Substring(0, 80) + "..." : question.Content
                })
                .ToList();
        }

        private IReadOnlyList<CrudOptionViewModel> BuildExamOptions()
        {
            IQueryable<Exam> query = Context.Exams.AsNoTracking().Include(exam => exam.Class);
            if (!IsAdmin)
            {
                query = query.Where(exam => exam.CreatedById == CurrentUserId || exam.Class.TeacherId == CurrentUserId);
            }

            return query
                .OrderByDescending(exam => exam.StartAt)
                .Take(200)
                .Select(exam => new CrudOptionViewModel
                {
                    Value = exam.Id.ToString(CultureInfo.InvariantCulture),
                    Text = exam.Title
                })
                .ToList();
        }

        private IReadOnlyList<CrudOptionViewModel> BuildAttemptOptions()
        {
            IQueryable<ExamAttempt> query = Context.ExamAttempts.AsNoTracking().Include(attempt => attempt.Exam).ThenInclude(exam => exam.Class);
            if (!IsAdmin)
            {
                query = query.Where(attempt => attempt.UserId == CurrentUserId
                    || attempt.Exam.CreatedById == CurrentUserId
                    || attempt.Exam.Class.TeacherId == CurrentUserId);
            }

            return query
                .OrderByDescending(attempt => attempt.StartedAt)
                .Take(200)
                .Select(attempt => new CrudOptionViewModel
                {
                    Value = attempt.Id.ToString(CultureInfo.InvariantCulture),
                    Text = "#" + attempt.Id + " - " + attempt.Status
                })
                .ToList();
        }

        private IReadOnlyList<CrudOptionViewModel> BuildUserOptions()
        {
            return Context.Users
                .AsNoTracking()
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .Take(300)
                .Select(user => new CrudOptionViewModel
                {
                    Value = user.Id,
                    Text = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? user.UserName ?? user.Id : user.FullName
                })
                .ToList();
        }

        private static Type GetUnderlyingType(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static HashSet<string> GetConfiguredFields(Dictionary<string, string[]> source, string entityName)
        {
            if (source.TryGetValue(entityName, out var fields))
            {
                return fields.ToHashSet();
            }

            return new HashSet<string>();
        }

        private static string BuildPlaceholder(string propertyName)
        {
            if (propertyName.EndsWith("Id", StringComparison.Ordinal))
            {
                return "Chon du lieu lien ket";
            }

            return FieldLabels.TryGetValue(propertyName, out var label) ? label : SplitPascalCase(propertyName);
        }

        private static string? BuildHelpText(string entityName, string propertyName)
        {
            if (entityName == nameof(Exam) && propertyName == nameof(Exam.ClassId))
            {
                return "Chi hien cac lop/phong do ban tao. Neu danh sach trong, hay tao lop hoc truoc.";
            }

            if (entityName == nameof(Class) && propertyName == nameof(Class.SubjectId))
            {
                return "Database moi se co san mon hoc mac dinh.";
            }

            return null;
        }

        private static bool IsServerAssignedField(string entityName, string propertyName)
        {
            return propertyName == nameof(Class.TeacherId)
                || propertyName == nameof(Question.CreatedById)
                || propertyName == nameof(Exam.CreatedById);
        }

        private static bool IsGeneratedKey(IProperty property, bool isKey)
        {
            return isKey && property.ValueGenerated == ValueGenerated.OnAdd;
        }

        private static string SplitPascalCase(string value)
        {
            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }

        private static string FormatEnumName(Type type, string name)
        {
            var value = Enum.Parse(type, name);
            return value switch
            {
                ExamStatus status => status.ToVietnamese(),
                ExamAttemptStatus status => status.ToVietnamese(),
                ResultReleaseMode mode => mode.ToVietnamese(),
                QuestionType questionType => questionType.ToVietnamese(),
                QuestionDifficulty difficulty => difficulty.ToVietnamese(),
                AntiCheatEventType eventType => eventType.ToVietnamese(),
                _ => SplitPascalCase(name)
            };
        }
    }
}
