using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccessPolicy _accessPolicy;

        public ChatController(ApplicationDbContext context, IAccessPolicy accessPolicy)
        {
            _context = context;
            _accessPolicy = accessPolicy;
        }

        [HttpGet]
        public async Task<IActionResult> Room(string type, int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isAdmin = User.IsInRole("Admin");
            var normalizedType = type?.Trim().ToLowerInvariant();

            if (normalizedType == "class")
            {
                var classInfo = await _context.Classes.AsNoTracking()
                    .Where(item => item.Id == id)
                    .Select(item => new { item.Id, item.Name, SubjectName = item.Subject.Name })
                    .FirstOrDefaultAsync();
                if (classInfo is null)
                {
                    return NotFound();
                }
                if (!await _accessPolicy.CanAccessClassAsync(id, userId, isAdmin))
                {
                    return Forbid();
                }

                return View(new ChatRoomViewModel
                {
                    RoomType = "class",
                    RoomId = id,
                    Title = classInfo.Name,
                    Subtitle = $"Trao đổi trực tiếp · {classInfo.SubjectName}",
                    BackUrl = Url.Action("Details", "Classes", new { id }) ?? "/"
                });
            }

            if (normalizedType == "exam")
            {
                var examInfo = await _context.Exams.AsNoTracking()
                    .Where(item => item.Id == id)
                    .Select(item => new { item.Id, item.Title, item.ClassId, ClassName = item.Class.Name })
                    .FirstOrDefaultAsync();
                if (examInfo is null)
                {
                    return NotFound();
                }
                if (!await _accessPolicy.CanAccessClassAsync(examInfo.ClassId, userId, isAdmin))
                {
                    return Forbid();
                }

                var canManage = await _accessPolicy.CanManageExamAsync(id, userId, isAdmin);
                return View(new ChatRoomViewModel
                {
                    RoomType = "exam",
                    RoomId = id,
                    Title = examInfo.Title,
                    Subtitle = $"Phòng trao đổi trực tiếp · {examInfo.ClassName}",
                    BackUrl = Url.Action(canManage ? "Manage" : "Room", "Exams", new { id }) ?? "/"
                });
            }

            return BadRequest();
        }
    }
}
