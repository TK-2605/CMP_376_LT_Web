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
                Key = id,
                Fields = BuildFields(entity, readOnlyKeys: true)
            });
        }

        public virtual IActionResult Create()
        {
            return SharedCrudView("Create", new CrudFormViewModel
            {
                Title = $"Create {GetTitle()}",
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
                    Title = $"Create {GetTitle()}",
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
                Title = $"Edit {GetTitle()}",
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
                    Title = $"Edit {GetTitle()}",
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
            return SplitPascalCase(typeof(TEntity).Name);
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

            return _entityType.GetProperties()
                .OrderBy(property => keyNames.Contains(property.Name) ? 0 : 1)
                .ThenBy(property => property.Name)
                .Select(property =>
                {
                    var clrProperty = GetProperty(property.Name);
                    var value = entity is null ? null : clrProperty.GetValue(entity);
                    var isKey = keyNames.Contains(property.Name);
                    return new CrudFieldViewModel
                    {
                        Name = property.Name,
                        Label = SplitPascalCase(property.Name),
                        Value = FormatInputValue(value, property.ClrType),
                        InputType = GetInputType(property.ClrType),
                        IsKey = isKey,
                        IsReadOnly = readOnlyKeys && isKey,
                        IsNullable = property.IsNullable,
                        IsBoolean = GetUnderlyingType(property.ClrType) == typeof(bool),
                        Options = BuildOptions(property.ClrType)
                    };
                })
                .ToList();
        }

        private void ApplyForm(TEntity entity, IFormCollection form, bool readOnlyKeys)
        {
            var keyNames = GetPrimaryKey().Properties.Select(property => property.Name).ToHashSet();

            foreach (var property in _entityType.GetProperties())
            {
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
                    ModelState.AddModelError(property.Name, $"{SplitPascalCase(property.Name)} is invalid.");
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

        private static string SplitPascalCase(string value)
        {
            return string.Concat(value.Select((character, index) =>
                index > 0 && char.IsUpper(character) ? " " + character : character.ToString()));
        }
    }
}
