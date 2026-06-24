using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ClassesController : CrudController<ClassEntity>
    {
        private readonly IUniqueCodeGenerator _codeGenerator;
        private readonly IPrivateMediaStorage _mediaStorage;

        public ClassesController(
            ApplicationDbContext context,
            IUniqueCodeGenerator codeGenerator,
            IPrivateMediaStorage mediaStorage) : base(context)
        {
            _codeGenerator = codeGenerator;
            _mediaStorage = mediaStorage;
        }

        protected override async Task OnCreatingAsync(ClassEntity entity)
        {
            entity.Code = await _codeGenerator.GenerateClassCodeAsync();
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public override async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (!int.TryParse(id, out var classId))
            {
                return NotFound();
            }

            var classRoom = await Context.Classes
                .Include(item => item.MediaAssets)
                .FirstOrDefaultAsync(item => item.Id == classId);
            if (classRoom is null)
            {
                return NotFound();
            }

            var mediaPaths = classRoom.MediaAssets.Select(item => item.Path)
                .Append(classRoom.CoverImagePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToList();

            var examIds = await Context.Exams
                .Where(item => item.ClassId == classId)
                .Select(item => item.Id)
                .ToListAsync();
            var attemptIds = await Context.ExamAttempts
                .Where(item => examIds.Contains(item.ExamId))
                .Select(item => item.Id)
                .ToListAsync();
            var answerIds = await Context.AttemptAnswers
                .Where(item => attemptIds.Contains(item.ExamAttemptId))
                .Select(item => item.Id)
                .ToListAsync();

            Context.AttemptAnswerSelections.RemoveRange(await Context.AttemptAnswerSelections
                .Where(item => answerIds.Contains(item.AttemptAnswerId))
                .ToListAsync());
            Context.AttemptAnswers.RemoveRange(await Context.AttemptAnswers
                .Where(item => attemptIds.Contains(item.ExamAttemptId))
                .ToListAsync());
            Context.AntiCheatEvents.RemoveRange(await Context.AntiCheatEvents
                .Where(item => attemptIds.Contains(item.ExamAttemptId))
                .ToListAsync());
            Context.ExamAttempts.RemoveRange(await Context.ExamAttempts
                .Where(item => examIds.Contains(item.ExamId))
                .ToListAsync());
            Context.ExamQuestions.RemoveRange(await Context.ExamQuestions
                .Where(item => examIds.Contains(item.ExamId))
                .ToListAsync());
            Context.ChatMessages.RemoveRange(await Context.ChatMessages
                .Where(item => item.RoomType == "class" && item.RoomId == classId
                    || item.RoomType == "exam" && examIds.Contains(item.RoomId))
                .ToListAsync());
            Context.Exams.RemoveRange(await Context.Exams
                .Where(item => item.ClassId == classId)
                .ToListAsync());
            Context.ClassMembers.RemoveRange(await Context.ClassMembers
                .Where(item => item.ClassId == classId)
                .ToListAsync());
            Context.ClassMedia.RemoveRange(classRoom.MediaAssets);
            Context.Classes.Remove(classRoom);

            await Context.SaveChangesAsync();
            foreach (var path in mediaPaths)
            {
                await _mediaStorage.DeleteAsync(path);
            }

            TempData["AdminMessage"] = "Đã xóa lớp học cùng lượt thi, đáp án, cảnh báo và thông báo liên quan.";
            return RedirectToAction(nameof(Index), new { area = AreaName });
        }
    }
}
