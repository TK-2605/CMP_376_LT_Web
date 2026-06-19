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
                        ["Roles"] = roles.Count > 0 ? string.Join(", ", roles.OrderBy(role => role)) : "Chưa có",
                        ["IsActive"] = user.IsActive ? "Đang hoạt động" : "Tạm khóa",
                        ["EmailConfirmed"] = user.EmailConfirmed ? "Đã xác thực" : "Chưa xác thực",
                        ["CreatedAt"] = user.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                });
            }

            return View("/Areas/Admin/Views/Shared/Crud/Index.cshtml", new CrudIndexViewModel
            {
                Title = "Tài khoản",
                Description = "Quản lý tài khoản, vai trò hệ thống và trạng thái truy cập.",
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
                Title = "Tài khoản",
                Description = "Thông tin liên hệ và trạng thái truy cập của tài khoản.",
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
                Title = "Thêm tài khoản",
                Description = "Tạo tài khoản mới và mật khẩu ban đầu cho người dùng.",
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
                Title = "Thêm tài khoản",
                Description = "Tạo tài khoản mới và mật khẩu ban đầu cho người dùng.",
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
                Title = "Sửa tài khoản",
                Description = "Cập nhật thông tin cá nhân và trạng thái truy cập.",
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
                Title = "Sửa tài khoản",
                Description = "Cập nhật thông tin cá nhân và trạng thái truy cập.",
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
                Title = "Tài khoản",
                Description = "Tài khoản sẽ không còn đăng nhập được sau khi bị xóa.",
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
                ModelState.AddModelError(string.Empty, "Hệ thống phải còn ít nhất một tài khoản Admin.");
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

            TempData["AdminMessage"] = "Đã cập nhật quyền cho tài khoản.";
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
                new() { Name = "FullName", Label = "Họ và tên", Value = user?.FullName, InputType = "text", Placeholder = "Nguyễn Văn A" },
                new() { Name = "StudentCode", Label = "Mã sinh viên", Value = user?.StudentCode, InputType = "text", IsNullable = true, Placeholder = "Nếu có" },
                new() { Name = "Roles", Label = "Quyền", Value = roles is null ? null : string.Join(", ", roles.OrderBy(role => role)), ShowInForm = false },
                new() { Name = "IsActive", Label = "Cho phép đăng nhập", Value = user?.IsActive == false ? "false" : "true", IsBoolean = true },
                new() { Name = "EmailConfirmed", Label = "Email đã xác thực", Value = user?.EmailConfirmed == true ? "true" : "false", IsBoolean = true }
            };

            if (includePassword)
            {
                fields.Insert(1, new CrudFieldViewModel
                {
                    Name = "Password",
                    Label = "Mật khẩu",
                    InputType = "password",
                    Placeholder = "Nhập mật khẩu ban đầu"
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
