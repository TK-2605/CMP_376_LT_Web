using System.Globalization;
using System.Reflection;
using LT_Web_Nhom4.Data;
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
            ["Subject"] = "Mon hoc",
            ["Class"] = "Lop hoc",
            ["ClassMember"] = "Thanh vien lop",
            ["Question"] = "Cau hoi",
            ["QuestionOption"] = "Lua chon dap an",
            ["Exam"] = "De thi",
            ["ExamQuestion"] = "Cau hoi trong de",
            ["ExamAttempt"] = "Luot lam bai",
            ["AttemptAnswer"] = "Dap an bai lam",
            ["AntiCheatEvent"] = "Canh bao bai thi"
        };

        private static readonly Dictionary<string, string> EntityDescriptions = new()
        {
            ["Subject"] = "Quan ly cac mon hoc dung de phan loai lop, cau hoi va de thi.",
            ["Class"] = "Quan ly lop hoc, hoc ky va giang vien phu trach.",
            ["ClassMember"] = "Quan ly sinh vien trong tung lop hoc.",
            ["Question"] = "Quan ly ngan hang cau hoi va trang thai su dung.",
            ["QuestionOption"] = "Quan ly cac lua chon cua tung cau hoi.",
            ["Exam"] = "Quan ly thong tin de thi, thoi gian va cau hinh lam bai.",
            ["ExamQuestion"] = "Gan cau hoi vao de thi va thiet lap diem.",
            ["ExamAttempt"] = "Theo doi cac luot lam bai cua sinh vien.",
            ["AttemptAnswer"] = "Xem dap an da luu va diem tung cau.",
            ["AntiCheatEvent"] = "Theo doi cac canh bao can giang vien xem lai."
        };

        private static readonly Dictionary<string, string[]> ListFields = new()
        {
            ["Subject"] = new[] { "Code", "Name", "Description" },
            ["Class"] = new[] { "Code", "Name", "Semester", "AcademicYear" },
            ["ClassMember"] = new[] { "ClassId", "UserId", "Status", "JoinedAt" },
            ["Question"] = new[] { "Content", "QuestionType", "Difficulty", "Status" },
            ["QuestionOption"] = new[] { "QuestionId", "Content", "IsCorrect", "DisplayOrder" },
            ["Exam"] = new[] { "Title", "DurationMinutes", "StartAt", "EndAt", "Status" },
            ["ExamQuestion"] = new[] { "ExamId", "QuestionId", "Score", "DisplayOrder" },
            ["ExamAttempt"] = new[] { "ExamId", "UserId", "StartedAt", "SubmittedAt", "Score", "Status" },
            ["AttemptAnswer"] = new[] { "ExamAttemptId", "QuestionId", "IsCorrect", "AwardedScore", "LastSavedAt" },
            ["AntiCheatEvent"] = new[] { "ExamAttemptId", "EventType", "Severity", "OccurredAt" }
        };

        private static readonly Dictionary<string, string[]> FormFields = new()
        {
            ["Subject"] = new[] { "Code", "Name", "Description" },
            ["Class"] = new[] { "SubjectId", "TeacherId", "Code", "Name", "Semester", "AcademicYear" },
            ["ClassMember"] = new[] { "ClassId", "UserId", "Status" },
            ["Question"] = new[] { "SubjectId", "CreatedById", "Content", "QuestionType", "Difficulty", "Explanation", "Status" },
            ["QuestionOption"] = new[] { "QuestionId", "Content", "IsCorrect", "DisplayOrder" },
            ["Exam"] = new[] { "SubjectId", "ClassId", "CreatedById", "Title", "DurationMinutes", "StartAt", "EndAt", "PassingScore", "ShuffleQuestions", "ShuffleOptions", "RequireFullscreen", "MaxTabSwitchCount", "Status" },
            ["ExamQuestion"] = new[] { "ExamId", "QuestionId", "Score", "DisplayOrder" },
            ["ExamAttempt"] = new[] { "ExamId", "UserId", "SubmittedAt", "Score", "Status", "IsAutoSubmitted" },
            ["AttemptAnswer"] = new[] { "ExamAttemptId", "QuestionId", "SelectedOptionId", "IsCorrect", "AwardedScore" },
            ["AntiCheatEvent"] = new[] { "ExamAttemptId", "EventType", "Severity", "Description", "OccurredAt" }
        };

        private static readonly Dictionary<string, string> FieldLabels = new()
        {
            ["AcademicYear"] = "Nam hoc",
            ["AwardedScore"] = "Diem dat duoc",
            ["ClassId"] = "Lop hoc",
            ["Code"] = "Ma",
            ["Content"] = "Noi dung",
            ["CreatedById"] = "Nguoi tao",
            ["Description"] = "Mo ta",
            ["Difficulty"] = "Do kho",
            ["DisplayOrder"] = "Thu tu hien thi",
            ["DurationMinutes"] = "Thoi luong",
            ["EndAt"] = "Ket thuc",
            ["EventType"] = "Loai su kien",
            ["ExamAttemptId"] = "Luot lam bai",
            ["ExamId"] = "De thi",
            ["Explanation"] = "Giai thich",
            ["IsAutoSubmitted"] = "Tu dong nop bai",
            ["IsCorrect"] = "Dap an dung",
            ["MaxTabSwitchCount"] = "So lan roi man hinh toi da",
            ["Name"] = "Ten",
            ["OccurredAt"] = "Thoi diem",
            ["PassingScore"] = "Diem dat",
            ["QuestionId"] = "Cau hoi",
            ["QuestionType"] = "Loai cau hoi",
            ["RequireFullscreen"] = "Yeu cau toan man hinh",
            ["Score"] = "Diem",
            ["SelectedOptionId"] = "Dap an da chon",
            ["Semester"] = "Hoc ky",
            ["Severity"] = "Muc do",
            ["ShuffleOptions"] = "Tron dap an",
            ["ShuffleQuestions"] = "Tron cau hoi",
            ["StartAt"] = "Bat dau",
            ["StartedAt"] = "Bat dau lam",
            ["Status"] = "Trang thai",
            ["SubjectId"] = "Mon hoc",
            ["SubmittedAt"] = "Thoi gian nop",
            ["TeacherId"] = "Giang vien",
            ["Title"] = "Tieu de",
            ["UserId"] = "Nguoi dung"
        };

        private readonly ApplicationDbContext _context;
        private readonly IEntityType _entityType;

        protected CrudController(ApplicationDbContext context)
        {
            _context = context;
            _entityType = context.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} is not part of ApplicationDbContext.");
        }

        public virtual async Task<IActionResult> Index()
        {
            var entities = await _context.Set<TEntity>().AsNoTracking().Take(200).ToListAsync();
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

            _context.Add(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        public virtual async Task<IActionResult> Edit(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
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

            ApplyForm(entity, form, readOnlyKeys: true);

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

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        public virtual async Task<IActionResult> Delete(string id)
        {
            var entity = await FindAsync(id);
            if (entity is null)
            {
                return NotFound();
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

            _context.Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }

        protected string ControllerName => ControllerContext.ActionDescriptor.ControllerName;

        protected string? AreaName => ControllerContext.ActionDescriptor.RouteValues.TryGetValue("area", out var area) ? area : null;

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
            return View($"/Views/Shared/Crud/{viewName}.cshtml", model);
        }

        private async Task<TEntity?> FindAsync(string id)
        {
            var keyValues = ParseKey(id);
            return await _context.Set<TEntity>().FindAsync(keyValues);
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
                    var showInList = listFields.Contains(property.Name);
                    var showInForm = formFields.Contains(property.Name) || isKey && !readOnlyKeys;
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
                        Options = BuildOptions(property.ClrType)
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
                if (!formFields.Contains(property.Name) && !(keyNames.Contains(property.Name) && !readOnlyKeys))
                {
                    continue;
                }

                if (readOnlyKeys && keyNames.Contains(property.Name))
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
                bool boolean => boolean ? "Co" : "Khong",
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

        private static IReadOnlyList<CrudOptionViewModel> BuildOptions(Type type)
        {
            var underlyingType = GetUnderlyingType(type);
            if (!underlyingType.IsEnum)
            {
                return new List<CrudOptionViewModel>();
            }

            return Enum.GetNames(underlyingType)
                .Select(name => new CrudOptionViewModel { Value = name, Text = SplitPascalCase(name) })
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
                return "Nhap ma lien ket";
            }

            return FieldLabels.TryGetValue(propertyName, out var label) ? label : SplitPascalCase(propertyName);
        }

        private static string SplitPascalCase(string value)
        {
            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }
    }
}
