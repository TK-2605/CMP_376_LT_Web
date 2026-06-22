using System.Collections.Concurrent;
using System.Security.Claims;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Hubs
{
    [Authorize]
    public sealed class QuizChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastMessageAt = new();
        private readonly ApplicationDbContext _context;
        private readonly IAccessPolicy _accessPolicy;

        public QuizChatHub(ApplicationDbContext context, IAccessPolicy accessPolicy)
        {
            _context = context;
            _accessPolicy = accessPolicy;
        }

        public async Task JoinRoom(string roomType, int roomId)
        {
            var groupName = await GetAuthorizedGroupAsync(roomType, roomId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task SendMessage(string roomType, int roomId, string message)
        {
            var normalizedMessage = message?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMessage) || normalizedMessage.Length > 500)
            {
                throw new HubException("Tin nhắn phải có từ 1 đến 500 ký tự.");
            }

            var now = DateTime.UtcNow;
            if (LastMessageAt.TryGetValue(Context.ConnectionId, out var previous)
                && now - previous < TimeSpan.FromMilliseconds(600))
            {
                throw new HubException("Bạn đang gửi tin nhắn quá nhanh.");
            }

            var groupName = await GetAuthorizedGroupAsync(roomType, roomId);
            LastMessageAt[Context.ConnectionId] = now;

            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var user = await _context.Users.AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new { item.FullName, item.Email })
                .FirstOrDefaultAsync();
            var sender = string.IsNullOrWhiteSpace(user?.FullName) ? user?.Email ?? "Thành viên" : user.FullName;
            var room = NormalizeRoomType(roomType);
            var savedMessage = new ChatMessage
            {
                RoomType = room,
                RoomId = roomId,
                SenderId = userId,
                SenderName = sender,
                Message = normalizedMessage,
                SentAt = DateTime.UtcNow
            };
            _context.ChatMessages.Add(savedMessage);
            await _context.SaveChangesAsync();

            await Clients.Group(groupName).SendAsync("ReceiveMessage", new
            {
                sender,
                message = normalizedMessage,
                sentAt = savedMessage.SentAt
            });
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            LastMessageAt.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        private async Task<string> GetAuthorizedGroupAsync(string roomType, int roomId)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var isAdmin = Context.User?.IsInRole("Admin") == true;
            var normalizedType = roomType?.Trim().ToLowerInvariant();

            if (normalizedType == "class"
                && await _accessPolicy.CanAccessClassAsync(roomId, userId, isAdmin))
            {
                return $"class:{roomId}";
            }

            if (normalizedType == "exam")
            {
                var classId = await _context.Exams.AsNoTracking()
                    .Where(item => item.Id == roomId)
                    .Select(item => (int?)item.ClassId)
                    .FirstOrDefaultAsync();
                if (classId.HasValue && await _accessPolicy.CanAccessClassAsync(classId.Value, userId, isAdmin))
                {
                    return $"exam:{roomId}";
                }
            }

            throw new HubException("Bạn không có quyền tham gia phòng chat này.");
        }

        private static string NormalizeRoomType(string roomType)
        {
            var normalized = roomType?.Trim().ToLowerInvariant();
            return normalized == "exam" ? "exam" : "class";
        }
    }
}
