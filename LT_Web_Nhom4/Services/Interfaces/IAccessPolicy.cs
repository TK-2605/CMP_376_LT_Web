namespace LT_Web_Nhom4.Services.Interfaces
{
    public interface IAccessPolicy
    {
        Task<bool> IsClassOwnerAsync(int classId, string userId, bool isAdmin = false);

        Task<bool> IsClassMemberAsync(int classId, string userId);

        Task<bool> CanAccessClassAsync(int classId, string userId, bool isAdmin = false);

        Task<bool> CanManageExamAsync(int examId, string userId, bool isAdmin = false);

        Task<bool> CanTakeExamAsync(int examId, string userId);
    }
}
