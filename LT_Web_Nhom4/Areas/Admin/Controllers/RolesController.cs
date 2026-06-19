using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RolesController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RolesController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .Where(role => role.Name != "Teacher")
                .ToListAsync();
            return View("/Areas/Admin/Views/Shared/Crud/Index.cshtml", new CrudIndexViewModel
            {
                Title = "Vai trò",
                Description = "Quản lý nhóm quyền dùng để phân cấp truy cập trong hệ thống.",
                ControllerName = "Roles",
                AreaName = "Admin",
                Fields = RoleFields(),
                Rows = roles.Select(role => new CrudRowViewModel
                {
                    Key = role.Id,
                    Values = new Dictionary<string, string?>
                    {
                        ["Name"] = role.Name
                    }
                }).ToList()
            });
        }

        public async Task<IActionResult> Details(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null || IsRoomTeacherRole(role.Name ?? string.Empty))
            {
                return NotFound();
            }

            return View("/Areas/Admin/Views/Shared/Crud/Details.cshtml", new CrudDetailsViewModel
            {
                Title = "Vai trò",
                Description = "Thông tin vai trò đang được sử dụng trong phân quyền.",
                ControllerName = "Roles",
                AreaName = "Admin",
                Key = role.Id,
                Fields = RoleFields(role)
            });
        }

        public IActionResult Create()
        {
            return View("/Areas/Admin/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Thêm vai trò",
                Description = "Đặt tên ngắn gọn, dễ hiểu cho nhóm quyền mới.",
                ActionName = nameof(Create),
                ControllerName = "Roles",
                AreaName = "Admin",
                Fields = RoleFields()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection form)
        {
            var roleName = form["Name"].ToString().Trim();
            if (IsRoomTeacherRole(roleName))
            {
                ModelState.AddModelError("Name", "Teacher là vai trò theo phòng thi, không tạo trong role hệ thống.");
                return View("/Areas/Admin/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
                {
                    Title = "Thêm vai trò",
                    Description = "Đặt tên ngắn gọn, dễ hiểu cho nhóm quyền mới.",
                    ActionName = nameof(Create),
                    ControllerName = "Roles",
                    AreaName = "Admin",
                    Fields = RoleFields(new IdentityRole(roleName))
                });
            }

            var role = new IdentityRole(roleName);
            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Areas/Admin/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Thêm vai trò",
                Description = "Đặt tên ngắn gọn, dễ hiểu cho nhóm quyền mới.",
                ActionName = nameof(Create),
                ControllerName = "Roles",
                AreaName = "Admin",
                Fields = RoleFields(role)
            });
        }

        public async Task<IActionResult> Edit(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null || IsRoomTeacherRole(role.Name ?? string.Empty))
            {
                return NotFound();
            }

            return View("/Areas/Admin/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Sửa vai trò",
                Description = "Cập nhật tên hiển thị của nhóm quyền.",
                ActionName = nameof(Edit),
                ControllerName = "Roles",
                AreaName = "Admin",
                Key = role.Id,
                Fields = RoleFields(role)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, IFormCollection form)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null || IsRoomTeacherRole(role.Name ?? string.Empty))
            {
                return NotFound();
            }

            var roleName = form["Name"].ToString().Trim();
            if (IsRoomTeacherRole(roleName))
            {
                ModelState.AddModelError("Name", "Teacher là vai trò theo phòng thi, không tạo trong role hệ thống.");
                return View("/Areas/Admin/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
                {
                    Title = "Sửa vai trò",
                    Description = "Cập nhật tên hiển thị của nhóm quyền.",
                    ActionName = nameof(Edit),
                    ControllerName = "Roles",
                    AreaName = "Admin",
                    Key = role.Id,
                    Fields = RoleFields(role)
                });
            }

            role.Name = roleName;
            var result = await _roleManager.UpdateAsync(role);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Areas/Admin/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Sửa vai trò",
                Description = "Cập nhật tên hiển thị của nhóm quyền.",
                ActionName = nameof(Edit),
                ControllerName = "Roles",
                AreaName = "Admin",
                Key = role.Id,
                Fields = RoleFields(role)
            });
        }

        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null || IsRoomTeacherRole(role.Name ?? string.Empty))
            {
                return NotFound();
            }

            return View("/Areas/Admin/Views/Shared/Crud/Delete.cshtml", new CrudDeleteViewModel
            {
                Title = "Vai trò",
                Description = "Chỉ xóa vai trò khi chắc chắn không còn được sử dụng.",
                ControllerName = "Roles",
                AreaName = "Admin",
                Key = role.Id,
                Fields = RoleFields(role)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null || IsRoomTeacherRole(role.Name ?? string.Empty))
            {
                return NotFound();
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return await Delete(id);
            }

            return RedirectToAction(nameof(Index));
        }

        private static IReadOnlyList<CrudFieldViewModel> RoleFields(IdentityRole? role = null)
        {
            return new List<CrudFieldViewModel>
            {
                new()
                {
                    Name = "Name",
                    Label = "Tên vai trò",
                    Value = role?.Name,
                    InputType = "text",
                    Placeholder = "Ví dụ: Admin, Student"
                }
            };
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
