using LT_Web_Nhom4.Areas.Admin.Models;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TechnologiesController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _context;
        private readonly IMeilisearchService _meilisearch;

        public TechnologiesController(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ApplicationDbContext context,
            IMeilisearchService meilisearch)
        {
            _configuration = configuration;
            _environment = environment;
            _context = context;
            _meilisearch = meilisearch;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var googleConfigured = HasValues("Authentication:Google:ClientId", "Authentication:Google:ClientSecret");
            var smtpConfigured = HasEmailProvider();
            var jwtKey = _configuration["Jwt:Key"];
            var jwtConfigured = !string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length >= 32;
            var meilisearch = await _meilisearch.GetHealthAsync(cancellationToken);
            var signalRClientPath = Path.Combine(_environment.WebRootPath, "vendor", "signalr", "signalr.min.js");
            var signalRReady = System.IO.File.Exists(signalRClientPath);
            var warningCount = await _context.AntiCheatEvents.CountAsync(cancellationToken);

            return View(new AdminTechnologyViewModel
            {
                Items =
                [
                    Item("Google OAuth 2.0", "Xác thực", "Đăng nhập và liên kết tài khoản Google qua ASP.NET Core Identity.",
                        googleConfigured ? "Đã nạp Client ID và Client Secret." : "Thêm secret Google để nút đăng nhập xuất hiện.",
                        "ri-google-fill", googleConfigured),
                    Item("OTP Gmail đăng ký", "Xác thực", "Xác nhận email bằng mã dùng một lần trước khi tạo tài khoản.",
                        smtpConfigured ? "SMTP Gmail đã sẵn sàng gửi mã." : "Luồng OTP đã có; SMTP chưa đủ thông tin đăng nhập.",
                        "ri-mail-check-line", smtpConfigured, Url.Action("Register", "Account", new { area = "Identity" }), "Mở đăng ký"),
                    Item("OTP quên mật khẩu", "Bảo mật", "Mã OTP 6 số, hết hạn sau 5 phút và giới hạn 5 lần nhập.",
                        smtpConfigured ? "Luồng gửi, xác minh và đổi mật khẩu đã sẵn sàng." : "Luồng đã có; môi trường phát triển sẽ hiển thị mã khi SMTP trống.",
                        "ri-key-2-line", smtpConfigured, Url.Action("ForgotPassword", "Account", new { area = "Identity" }), "Mở khôi phục"),
                    Item("JWT & Refresh Token", "API", "Access token ký HMAC, refresh token chỉ lưu bản băm và được xoay vòng.",
                        jwtConfigured ? "Khóa ký hợp lệ; API /api/auth đã sẵn sàng." : "Đặt Jwt:Key tối thiểu 32 ký tự trong User Secrets.",
                        "ri-shield-keyhole-line", jwtConfigured, "/swagger", "Mở Swagger"),
                    new AdminTechnologyItemViewModel
                    {
                        Name = "Meilisearch AutoComplete",
                        Category = "Tìm kiếm",
                        Description = "Tìm gần đúng lớp và đề trong đúng phạm vi người dùng được phép xem.",
                        Detail = meilisearch.Message,
                        Icon = "ri-search-eye-line",
                        State = meilisearch.Reachable ? TechnologyState.Ready : TechnologyState.Fallback
                    },
                    Item("SignalR chat socket", "Thời gian thực", "Chat theo nhóm lớp/phòng thi, kiểm tra thành viên và không lưu lịch sử.",
                        signalRReady ? "Hub và browser client đã sẵn sàng." : "Thiếu browser client SignalR trong wwwroot/vendor.",
                        "ri-chat-3-line", signalRReady),
                    new AdminTechnologyItemViewModel
                    {
                        Name = "Chống gian lận",
                        Category = "Giám sát",
                        Description = "Ghi nhận chuyển tab, mất focus, thoát toàn màn hình, sao chép và dán.",
                        Detail = $"Đang hoạt động · đã lưu {warningCount} sự kiện.",
                        Icon = "ri-alarm-warning-line",
                        State = TechnologyState.Ready,
                        ActionUrl = Url.Action("Index", "AntiCheatEvents", new { area = "Admin" }),
                        ActionLabel = "Xem cảnh báo"
                    },
                    new AdminTechnologyItemViewModel
                    {
                        Name = "Chuyển đổi ngôn ngữ",
                        Category = "Giao diện",
                        Description = "Request Localization bằng cookie, hỗ trợ tiếng Việt, Anh, Nhật, Hàn và Trung.",
                        Detail = "Header và màn hình xác thực đã dùng tài nguyên ngôn ngữ chung.",
                        Icon = "ri-translate-2",
                        State = TechnologyState.Ready
                    }
                ]
            });
        }

        private bool HasValues(params string[] keys)
        {
            return keys.All(key => !string.IsNullOrWhiteSpace(_configuration[key]));
        }

        private bool HasEmailProvider()
        {
            return HasValues("Smtp:Host", "Smtp:UserName", "Smtp:Password", "Smtp:FromEmail")
                || HasValues("Resend:ApiKey", "Resend:FromEmail");
        }

        private static AdminTechnologyItemViewModel Item(
            string name,
            string category,
            string description,
            string detail,
            string icon,
            bool ready,
            string? actionUrl = null,
            string? actionLabel = null)
        {
            return new AdminTechnologyItemViewModel
            {
                Name = name,
                Category = category,
                Description = description,
                Detail = detail,
                Icon = icon,
                State = ready ? TechnologyState.Ready : TechnologyState.NeedsConfiguration,
                ActionUrl = actionUrl,
                ActionLabel = actionLabel
            };
        }
    }
}
