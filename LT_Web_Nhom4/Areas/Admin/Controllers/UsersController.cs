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

        public UsersController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.AsNoTracking().ToListAsync();
            return View("/Views/Shared/Crud/Index.cshtml", new CrudIndexViewModel
            {
                Title = "Users",
                ControllerName = "Users",
                AreaName = "Admin",
                Fields = UserFields(),
                Rows = users.Select(user => new CrudRowViewModel
                {
                    Key = user.Id,
                    Values = new Dictionary<string, string?>
                    {
                        ["Email"] = user.Email,
                        ["FullName"] = user.FullName,
                        ["StudentCode"] = user.StudentCode,
                        ["IsActive"] = user.IsActive ? "True" : "False",
                        ["EmailConfirmed"] = user.EmailConfirmed ? "True" : "False",
                        ["CreatedAt"] = user.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                }).ToList()
            });
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            return View("/Views/Shared/Crud/Details.cshtml", new CrudDetailsViewModel
            {
                Title = "User",
                ControllerName = "Users",
                AreaName = "Admin",
                Key = user.Id,
                Fields = UserFields(user, includePassword: false)
            });
        }

        public IActionResult Create()
        {
            return View("/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Create User",
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
            return View("/Views/Shared/Crud/Create.cshtml", new CrudFormViewModel
            {
                Title = "Create User",
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

            return View("/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Edit User",
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
            return View("/Views/Shared/Crud/Edit.cshtml", new CrudFormViewModel
            {
                Title = "Edit User",
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

            return View("/Views/Shared/Crud/Delete.cshtml", new CrudDeleteViewModel
            {
                Title = "User",
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

        private static IReadOnlyList<CrudFieldViewModel> UserFields(ApplicationUser? user = null, bool includePassword = false)
        {
            var fields = new List<CrudFieldViewModel>
            {
                new() { Name = "Email", Label = "Email", Value = user?.Email, InputType = "text" },
                new() { Name = "FullName", Label = "Full Name", Value = user?.FullName, InputType = "text" },
                new() { Name = "StudentCode", Label = "Student Code", Value = user?.StudentCode, InputType = "text", IsNullable = true },
                new() { Name = "IsActive", Label = "Is Active", Value = user?.IsActive == false ? "false" : "true", IsBoolean = true },
                new() { Name = "EmailConfirmed", Label = "Email Confirmed", Value = user?.EmailConfirmed == true ? "true" : "false", IsBoolean = true }
            };

            if (includePassword)
            {
                fields.Insert(1, new CrudFieldViewModel { Name = "Password", Label = "Password", InputType = "password" });
            }

            return fields;
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
