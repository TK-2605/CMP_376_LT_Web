using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [Authorize]
    public class MediaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccessPolicy _accessPolicy;
        private readonly IPrivateMediaStorage _mediaStorage;

        public MediaController(
            ApplicationDbContext context,
            IAccessPolicy accessPolicy,
            IPrivateMediaStorage mediaStorage)
        {
            _context = context;
            _accessPolicy = accessPolicy;
            _mediaStorage = mediaStorage;
        }

        [HttpGet]
        public async Task<IActionResult> ClassCover(int id, CancellationToken cancellationToken)
        {
            if (!await _accessPolicy.CanAccessClassAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var path = await _context.Classes.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.CoverImagePath)
                .FirstOrDefaultAsync(cancellationToken);
            return await BuildFileResultAsync(path, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> ClassMedia(int id, int mediaId, CancellationToken cancellationToken)
        {
            if (!await _accessPolicy.CanAccessClassAsync(id, CurrentUserId, IsAdmin))
            {
                return Forbid();
            }

            var path = await _context.ClassMedia.AsNoTracking()
                .Where(item => item.ClassId == id && item.Id == mediaId)
                .Select(item => item.Path)
                .FirstOrDefaultAsync(cancellationToken);
            return await BuildFileResultAsync(path, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> QuestionImage(int id, CancellationToken cancellationToken)
        {
            var question = await _context.Questions.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.CreatedById,
                    item.ImagePath,
                    CanView = item.ExamQuestions.Any(link =>
                        (link.Exam.Status != ExamStatus.Draft && link.Exam.Status != ExamStatus.Cancelled)
                        && link.Exam.Class.Members.Any(member =>
                            member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active))
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (question is null)
            {
                return NotFound();
            }

            if (!IsAdmin && question.CreatedById != CurrentUserId && !question.CanView)
            {
                return Forbid();
            }

            return await BuildFileResultAsync(question.ImagePath, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> QuestionMedia(int id, int mediaId, CancellationToken cancellationToken)
        {
            var media = await _context.QuestionMedia.AsNoTracking()
                .Where(item => item.QuestionId == id && item.Id == mediaId)
                .Select(item => new
                {
                    item.Path,
                    item.Question.CreatedById,
                    CanView = item.Question.ExamQuestions.Any(link =>
                        (link.Exam.Status != ExamStatus.Draft && link.Exam.Status != ExamStatus.Cancelled)
                        && link.Exam.Class.Members.Any(member =>
                            member.UserId == CurrentUserId && member.Status == ClassMemberStatus.Active))
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (media is null)
            {
                return NotFound();
            }

            if (!IsAdmin && media.CreatedById != CurrentUserId && !media.CanView)
            {
                return Forbid();
            }

            return await BuildFileResultAsync(media.Path, cancellationToken);
        }

        private async Task<IActionResult> BuildFileResultAsync(string? path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return NotFound();
            }

            var stream = await _mediaStorage.OpenReadAsync(path, cancellationToken);
            return stream is null ? NotFound() : File(stream, _mediaStorage.GetContentType(path));
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private bool IsAdmin => User.IsInRole("Admin");
    }
}
