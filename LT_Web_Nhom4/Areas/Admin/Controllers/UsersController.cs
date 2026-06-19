using LT_Web_Nhom4.Areas.Admin.Models;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.OrderByDescending(user => user.CreatedAt).ToListAsync();
            var rows = new List<CrudRowViewModel>();

            foreach (var user in users)
            {
                var roles = (await _userManager.GetRolesAsync(user))
                    .Where(role => !IsRoomTeacherRole(role))
                    .ToList();
                rows.Add(new CrudRowViewModel
                {
                    Key = user.Id,
                    Values = new Dictionary<string, string?>
                    {
                        ["Email"] = user.Email,
                        ["FullName"] = user.FullName,
                        ["StudentCode"] = user.StudentCode,
                        ["Roles"] = roles.Count > 0 ? string.Join(", ", roles.OrderBy(role => role)) : "Chua co",
                        ["IsActive"] = user.IsActive ? "Dang hoat dong" : "Tam khoa",
                        ["EmailConfirmed"] = user.EmailConfirmed ? "Da xac thuc" : "Chua xac thuc",
                        ["CreatedAt"] = user.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }

            return View("/Areas/Admin/Views/Shared/Crud/Index.cshtml", new CrudIndexViewModel
            {
                Title = "Tai khoan",
                Description = "Quan ly tai khoan sinh vien, giang vien va trang thai truy cap.",
                ControllerName = "Users",
                AreaName = "Admin",
                Fields = UserFields(),
                Rows = rows
            });
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var roles = (await _userManager.GetRolesAsync(user))
                .Where(role => !IsRoomTeacherRole(role))
                .ToList();

            return View("/Areas/Admin/Views/Shared/Crud/Details.cshtml", new CrudDetailsViewModel
            {
                Title = "Tai khoan",
                Description = "Thong tin lien he va trang thai truy cap cua tai khoan.",
                ControllerName = "Users",
                AreaName = "Admin",
                Key = user.Id,
                Fields = UserFields(user, includePassword: false, roles)
            });
        }

        public IActionResult Create()
        {
            return View("/Areas/Admin/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Them tai khoan",
                Description = "Tao tai khoan moi va mat khau ban dau cho nguoi dung.",
                ActionName = nameof(Create),
                ControllerName = "Users",
                AreaName = "Admin",
                Fields = UserFields(includePassword: true)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection form)
        {
            var user = new ApplicationUser
            {
                UserName = form["Email"].ToString(),
                Email = form["Email"].ToString(),
                FullName = form["FullName"].ToString(),
                StudentCode = string.IsNullOrWhiteSpace(form["StudentCode"]) ? null : form["StudentCode"].ToString(),
                IsActive = form["IsActive"].Contains("true"),
                EmailConfirmed = form["EmailConfirmed"].Contains("true")
            };

            var result = await _userManager.CreateAsync(user, form["Password"].ToString());
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Areas/Admin/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Them tai khoan",
                Description = "Tao tai khoan moi va mat khau ban dau cho nguoi dung.",
                ActionName = nameof(Create),
                ControllerName = "Users",
                AreaName = "Admin",
                Fields = UserFields(user, includePassword: true)
            });
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            return View("/Areas/Admin/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Sua tai khoan",
                Description = "Cap nhat thong tin ca nhan va trang thai truy cap.",
                ActionName = nameof(Edit),
                ControllerName = "Users",
                AreaName = "Admin",
                Key = user.Id,
                Fields = UserFields(user, includePassword: false)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, IFormCollection form)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            user.UserName = form["Email"].ToString();
            user.Email = form["Email"].ToString();
            user.FullName = form["FullName"].ToString();
            user.StudentCode = string.IsNullOrWhiteSpace(form["StudentCode"]) ? null : form["StudentCode"].ToString();
            user.IsActive = form["IsActive"].Contains("true");
            user.EmailConfirmed = form["EmailConfirmed"].Contains("true");

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Areas/Admin/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Sua tai khoan",
                Description = "Cap nhat thong tin ca nhan va trang thai truy cap.",
                ActionName = nameof(Edit),
                ControllerName = "Users",
                AreaName = "Admin",
                Key = user.Id,
                Fields = UserFields(user, includePassword: false)
            });
        }

        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            return View("/Areas/Admin/Views/Shared/Crud/Delete.cshtml", new CrudDeleteViewModel
            {
                Title = "Tai khoan",
                Description = "Tai khoan se khong con dang nhap duoc sau khi bi xoa.",
                ControllerName = "Users",
                AreaName = "Admin",
                Key = user.Id,
                Fields = UserFields(user, includePassword: false)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return await Delete(id);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ManageRoles(string id)
        {
            var model = await BuildRoleAssignmentModelAsync(id);
            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageRoles(AdminRoleAssignmentViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user is null)
            {
                return NotFound();
            }

            var availableRoles = await GetAvailableRoleNamesAsync();
            var selectedRoles = model.SelectedRoles
                .Where(role => availableRoles.Contains(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var currentRoles = await _userManager.GetRolesAsync(user);

            if (currentRoles.Contains("Admin") && !selectedRoles.Contains("Admin") && !await CanRemoveAdminRoleAsync(user.Id))
            {
                ModelState.AddModelError(string.Empty, "He thong phai con it nhat mot tai khoan Admin.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildRoleAssignmentModelAsync(user.Id, selectedRoles);
                return invalidModel is null ? NotFound() : View(invalidModel);
            }

            var rolesToRemove = currentRoles.Except(selectedRoles, StringComparer.OrdinalIgnoreCase).ToList();
            var rolesToAdd = selectedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    AddErrors(removeResult);
                    var invalidModel = await BuildRoleAssignmentModelAsync(user.Id, selectedRoles);
                    return invalidModel is null ? NotFound() : View(invalidModel);
                }
            }

            if (rolesToAdd.Count > 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    AddErrors(addResult);
                    var invalidModel = await BuildRoleAssignmentModelAsync(user.Id, selectedRoles);
                    return invalidModel is null ? NotFound() : View(invalidModel);
                }
            }

            TempData["AdminMessage"] = "Da cap nhat quyen cho tai khoan.";
            return RedirectToAction(nameof(Index));
        }

        private static IReadOnlyList<CrudFieldViewModel> UserFields(
            ApplicationUser? user = null,
            bool includePassword = false,
            IEnumerable<string>? roles = null)
        {
            var fields = new List<CrudFieldViewModel>
            {
                new() { Name = "Email", Label = "Email", Value = user?.Email, InputType = "email", Placeholder = "name@example.com" },
                new() { Name = "FullName", Label = "Ho va ten", Value = user?.FullName, InputType = "text", Placeholder = "Nguyen Van A" },
                new() { Name = "StudentCode", Label = "Ma sinh vien", Value = user?.StudentCode, InputType = "text", IsNullable = true, Placeholder = "Neu co" },
                new() { Name = "Roles", Label = "Quyen", Value = roles is null ? null : string.Join(", ", roles.OrderBy(role => role)), ShowInForm = false },
                new() { Name = "IsActive", Label = "Cho phep dang nhap", Value = user?.IsActive == false ? "false" : "true", IsBoolean = true },
                new() { Name = "EmailConfirmed", Label = "Email da xac thuc", Value = user?.EmailConfirmed == true ? "true" : "false", IsBoolean = true }
            };

            if (includePassword)
            {
                fields.Insert(1, new CrudFieldViewModel
                {
                    Name = "Password",
                    Label = "Mat khau",
                    InputType = "password",
                    Placeholder = "Nhap mat khau ban dau"
                });
            }

            return fields;
        }

        private async Task<AdminRoleAssignmentViewModel?> BuildRoleAssignmentModelAsync(string id, IEnumerable<string>? selectedRoles = null)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return null;
            }

            var currentRoles = selectedRoles?.ToList() ?? (await _userManager.GetRolesAsync(user)).ToList();

            return new AdminRoleAssignmentViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                StudentCode = user.StudentCode,
                AvailableRoles = await GetAvailableRoleNamesAsync(),
                SelectedRoles = currentRoles.Where(role => !IsRoomTeacherRole(role)).ToList()
            };
        }

        private async Task<IList<string>> GetAvailableRoleNamesAsync()
        {
            return await _roleManager.Roles
                .AsNoTracking()
                .Where(role => role.Name != null)
                .Select(role => role.Name!)
                .Where(role => !IsRoomTeacherRole(role))
                .OrderBy(role => role)
                .ToListAsync();
        }

        private async Task<bool> CanRemoveAdminRoleAsync(string userId)
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            return adminUsers.Any(user => user.Id != userId);
        }

        private static bool IsRoomTeacherRole(string role)
        {
            return string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
