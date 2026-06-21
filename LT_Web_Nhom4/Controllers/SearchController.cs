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
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMeilisearchService _meilisearch;

        public SearchController(ApplicationDbContext context, IMeilisearchService meilisearch)
        {
            _context = context;
            _meilisearch = meilisearch;
        }

        [HttpGet]
        public async Task<IActionResult> Suggestions(string? q, CancellationToken cancellationToken)
        {
            var term = q?.Trim();
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Json(Array.Empty<object>());
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isAdmin = User.IsInRole("Admin");
            var classes = await _context.Classes.AsNoTracking()
                .Where(item => isAdmin
                    || item.TeacherId == userId
                    || item.Members.Any(member => member.UserId == userId && member.Status == ClassMemberStatus.Active))
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    SubjectName = item.Subject.Name,
                    item.Code
                })
                .ToListAsync(cancellationToken);

            var exams = await _context.Exams.AsNoTracking()
                .Where(item => isAdmin
                    || item.CreatedById == userId
                    || item.Class.TeacherId == userId
                    || (item.Status == ExamStatus.Published
                        && item.Class.Members.Any(member => member.UserId == userId && member.Status == ClassMemberStatus.Active)))
                .Select(item => new
                {
                    item.Id,
                    item.Title,
                    ClassName = item.Class.Name,
                    item.Code,
                    CanManage = isAdmin || item.CreatedById == userId || item.Class.TeacherId == userId
                })
                .ToListAsync(cancellationToken);

            var documents = classes.Select(item => new SearchIndexDocument(
                    $"class-{item.Id}", "class", item.Id, item.Name, $"{item.SubjectName} · {item.Code}"))
                .Concat(exams.Select(item => new SearchIndexDocument(
                    $"exam-{item.Id}", "exam", item.Id, item.Title, $"{item.ClassName} · {item.Code}")))
                .ToList();

            var meilisearchHits = await _meilisearch.SearchAsync(term, documents, cancellationToken);
            if (meilisearchHits is not null)
            {
                return Json(meilisearchHits.Select(hit => new
                {
                    type = hit.Type,
                    title = hit.Title,
                    meta = hit.Meta,
                    url = BuildUrl(hit.Type, hit.EntityId, exams.FirstOrDefault(item => item.Id == hit.EntityId)?.CanManage == true)
                }));
            }

            var localResults = documents
                .Where(item => item.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                    || item.Meta.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                .Take(8)
                .Select(item => new
                {
                    type = item.Type,
                    title = item.Title,
                    meta = item.Meta,
                    url = BuildUrl(item.Type, item.EntityId, exams.FirstOrDefault(exam => exam.Id == item.EntityId)?.CanManage == true)
                });

            return Json(localResults);
        }

        private string? BuildUrl(string type, int id, bool canManage)
        {
            return type == "class"
                ? Url.Action("Details", "Classes", new { id })
                : Url.Action(canManage ? "Manage" : "Room", "Exams", new { id });
        }
    }
}
