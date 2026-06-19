using System.Security.Claims;
using System.Text.Json;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class ExamSecurityController : Controller
    {
        private static readonly AntiCheatEventType[] ClientEventTypes =
        {
            AntiCheatEventType.TabHidden,
            AntiCheatEventType.WindowBlur,
            AntiCheatEventType.FullscreenExited
        };

        private readonly ApplicationDbContext _context;

        public ExamSecurityController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report([FromBody] AntiCheatReportViewModel model)
        {
            if (!ModelState.IsValid || !ClientEventTypes.Contains(model.EventType))
            {
                return BadRequest(new { ok = false, message = "Invalid anti-cheat event." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var attemptExists = await _context.ExamAttempts
                .AsNoTracking()
                .AnyAsync(attempt =>
                    attempt.Id == model.ExamAttemptId &&
                    attempt.ExamId == model.ExamId &&
                    attempt.UserId == userId &&
                    attempt.Status == ExamAttemptStatus.InProgress);

            if (!attemptExists)
            {
                return BadRequest(new { ok = false, message = "The active exam attempt was not found." });
            }

            var violationCount = await _context.AntiCheatEvents
                .CountAsync(antiCheatEvent => antiCheatEvent.ExamAttemptId == model.ExamAttemptId) + 1;
            var isSuspicious = violationCount > 3;
            var now = DateTime.UtcNow;

            var antiCheatEvent = new AntiCheatEvent
            {
                UserId = userId,
                ExamId = model.ExamId,
                ExamAttemptId = model.ExamAttemptId,
                EventType = model.EventType,
                ViolationCount = violationCount,
                IsSuspicious = isSuspicious,
                Severity = isSuspicious
                    ? AntiCheatSeverity.High
                    : violationCount == 3 ? AntiCheatSeverity.Medium : AntiCheatSeverity.Low,
                Note = model.Note,
                Description = model.Note,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                }),
                OccurredAt = now,
                CreatedAt = now
            };

            _context.AntiCheatEvents.Add(antiCheatEvent);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                ok = true,
                violationCount,
                isSuspicious,
                eventId = antiCheatEvent.Id,
                createdAt = antiCheatEvent.CreatedAt
            });
        }
    }
}
