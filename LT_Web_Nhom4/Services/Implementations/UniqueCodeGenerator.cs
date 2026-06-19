using System.Security.Cryptography;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class UniqueCodeGenerator : IUniqueCodeGenerator
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private readonly ApplicationDbContext _context;

        public UniqueCodeGenerator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateClassCodeAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = $"{RandomSegment(3)}-{RandomSegment(3)}-{RandomSegment(3)}";
                if (!await _context.Classes.AnyAsync(item => item.Code == candidate, cancellationToken))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Không thể tạo mã lớp duy nhất. Vui lòng thử lại.");
        }

        public async Task<string> GenerateExamCodeAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = $"DE-{RandomSegment(6)}";
                if (!await _context.Exams.AnyAsync(item => item.Code == candidate, cancellationToken))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Không thể tạo mã đề duy nhất. Vui lòng thử lại.");
        }

        private static string RandomSegment(int length)
        {
            return string.Create(length, 0, static (buffer, _) =>
            {
                for (var index = 0; index < buffer.Length; index++)
                {
                    buffer[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
                }
            });
        }
    }
}
