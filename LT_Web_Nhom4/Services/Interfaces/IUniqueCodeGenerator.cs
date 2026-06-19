namespace LT_Web_Nhom4.Services.Interfaces
{
    public interface IUniqueCodeGenerator
    {
        Task<string> GenerateClassCodeAsync(CancellationToken cancellationToken = default);

        Task<string> GenerateExamCodeAsync(CancellationToken cancellationToken = default);
    }
}
