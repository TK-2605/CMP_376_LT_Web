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
            var roles = await _roleManager.Roles.AsNoTracking().ToListAsync();
            return View("/Views/Shared/Crud/Index.cshtml", new CrudIndexViewModel
            {
                Title = "Roles",
                ControllerName = "Roles",
                AreaName = "Admin",
                Fields = RoleFields(),
                Rows = roles.Select(role => new CrudRowViewModel
                {
                    Key = role.Id,
                    Values = new Dictionary<string, string?>
                    {
                        ["Id"] = role.Id,
                        ["Name"] = role.Name,
                        ["NormalizedName"] = role.NormalizedName
                    }
                }).ToList()
            });
        }

        public async Task<IActionResult> Details(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
            {
                return NotFound();
            }

            return View("/Views/Shared/Crud/Details.cshtml", new CrudDetailsViewModel
            {
                Title = "Role",
                ControllerName = "Roles",
                AreaName = "Admin",
                Key = role.Id,
                Fields = RoleFields(role)
            });
        }

        public IActionResult Create()
        {
            return View("/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Create Role",
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
            var role = new IdentityRole(form["Name"].ToString());
            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Create Role",
                ActionName = nameof(Create),
                ControllerName = "Roles",
                AreaName = "Admin",
                Fields = RoleFields(role)
            });
        }

        public async Task<IActionResult> Edit(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null)
            {
                return NotFound();
            }

            return View("/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Edit Role",
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
            if (role is null)
            {
                return NotFound();
            }

            role.Name = form["Name"];
            var result = await _roleManager.UpdateAsync(role);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            AddErrors(result);
            return View("/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Edit Role",
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
            if (role is null)
            {
                return NotFound();
            }

            return View("/Views/Shared/Crud/Delete.cshtml", new CrudDeleteViewModel
            {
                Title = "Role",
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
            if (role is null)
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
                new() { Name = "Id", Label = "Id", Value = role?.Id, IsReadOnly = true },
                new() { Name = "Name", Label = "Name", Value = role?.Name, InputType = "text" },
                new() { Name = "NormalizedName", Label = "Normalized Name", Value = role?.NormalizedName, IsReadOnly = true }
            };
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
