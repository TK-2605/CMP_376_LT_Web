using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuestionsController : CrudController<Question>
    {
        private readonly IPrivateMediaStorage _mediaStorage;

        public QuestionsController(ApplicationDbContext context, IPrivateMediaStorage mediaStorage) : base(context)
        {
            _mediaStorage = mediaStorage;
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (!int.TryParse(id, out var questionId))
            {
                return NotFound();
            }

            var question = await Context.Questions
                .Include(item => item.MediaAssets)
                .FirstOrDefaultAsync(item => item.Id == questionId);
            if (question is null)
            {
                return NotFound();
            }

            var isUsed = await Context.ExamQuestions.AnyAsync(item => item.QuestionId == questionId)
                || await Context.AttemptAnswers.AnyAsync(item => item.QuestionId == questionId);
            if (isUsed)
            {
                TempData["AdminMessage"] = "Câu hỏi đã nằm trong đề thi hoặc bài làm nên không xóa cứng được. Hãy chuyển trạng thái câu hỏi sang nháp nếu muốn ngừng sử dụng.";
                return RedirectToAction(nameof(Index), new { area = AreaName });
            }

            var mediaPaths = question.MediaAssets.Select(item => item.Path)
                .Append(question.ImagePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToList();

            Context.QuestionMedia.RemoveRange(question.MediaAssets);
            Context.Questions.Remove(question);
            await Context.SaveChangesAsync();

            foreach (var path in mediaPaths)
            {
                await _mediaStorage.DeleteAsync(path);
            }

            TempData["AdminMessage"] = "Đã xóa câu hỏi.";
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }
    }
}
