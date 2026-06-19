using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class AccessPolicy : IAccessPolicy
    {
        private readonly ApplicationDbContext _context;

        public AccessPolicy(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsClassOwnerAsync(int classId, string userId, bool isAdmin = false)
        {
            return isAdmin
                ? Task.FromResult(true)
                : _context.Classes.AnyAsync(item => item.Id == classId && item.TeacherId == userId);
        }

        public Task<bool> IsClassMemberAsync(int classId, string userId)
        {
            return _context.ClassMembers.AnyAsync(item =>
                item.ClassId == classId && item.UserId == userId && item.Status == ClassMemberStatus.Active);
        }

        public async Task<bool> CanAccessClassAsync(int classId, string userId, bool isAdmin = false)
        {
            return isAdmin
                || await IsClassOwnerAsync(classId, userId)
                || await IsClassMemberAsync(classId, userId);
        }

        public Task<bool> CanManageExamAsync(int examId, string userId, bool isAdmin = false)
        {
            return isAdmin
                ? Task.FromResult(true)
                : _context.Exams.AnyAsync(item =>
                    item.Id == examId && (item.CreatedById == userId || item.Class.TeacherId == userId));
        }

        public Task<bool> CanTakeExamAsync(int examId, string userId)
        {
            return _context.Exams.AnyAsync(item =>
                item.Id == examId
                && item.Status == ExamStatus.Published
                && item.Class.Members.Any(member =>
                    member.UserId == userId && member.Status == ClassMemberStatus.Active));
        }
    }
}
