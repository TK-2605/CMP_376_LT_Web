using System.Security.Claims;
using System.Text.Json;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Models.ViewModels;
using LT_Web_Nhom4.Services;
using LT_Web_Nhom4.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LT_Web_Nhom4.Controllers
{
    [ApiController]
    [ApiTestOnly]
    [Route("api/features")]
    public class FeatureApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IMeilisearchService _meilisearch;

        public FeatureApiController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IMeilisearchService meilisearch)
        {
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _meilisearch = meilisearch;
        }

        [HttpGet("checklist")]
        [AllowAnonymous]
        public ActionResult<ApiTestPlanResponse> Checklist()
        {
            return Ok(new ApiTestPlanResponse
            {
                BaseUrl = $"{Request.Scheme}://{Request.Host}",
                Groups =
                [
                    new ApiTestGroup
                    {
                        Name = "Auth - Login JWT",
                        Purpose = "Test dang nhap API va Bearer token.",
                        Steps =
                        [
                            Step("GET", "/api/auth/status", "Kiem tra cau hinh Google, email provider, JWT.", false),
                            Step("POST", "/api/auth/login", "Dang nhap bang admin/student de lay accessToken va refreshToken.", false),
                            Step("GET", "/api/auth/me", "Kiem tra Bearer token da dung.", true),
                            Step("POST", "/api/auth/refresh", "Doi refresh token lay cap token moi.", false),
                            Step("POST", "/api/auth/revoke", "Thu hoi refresh token.", false)
                        ]
                    },
                    new ApiTestGroup
                    {
                        Name = "Auth - Register",
                        Purpose = "Test dang ky bang API tren localhost.",
                        Steps =
                        [
                            Step("POST", "/api/auth/register", "Tao pending registration va lay developmentCode khi chay local.", false),
                            Step("POST", "/api/auth/register/confirm", "Nhap code/token de tao user Student that trong LocalDB.", false)
                        ]
                    },
                    new ApiTestGroup
                    {
                        Name = "Auth - Forgot Password",
                        Purpose = "Test OTP quen mat khau tren localhost.",
                        Steps =
                        [
                            Step("POST", "/api/auth/forgot-password", "Tao OTP reset password; localhost tra developmentCode.", false),
                            Step("POST", "/api/auth/forgot-password/confirm", "Nhap OTP va mat khau moi de reset.", false)
                        ]
                    },
                    new ApiTestGroup
                    {
                        Name = "CRUD - LocalDB",
                        Purpose = "Test CRUD bang nghiep vu trong database mau.",
                        Steps =
                        [
                            Step("GET", "/api/admin-data/summary", "Xem DB provider va so dong cac bang mau.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/subjects", "CRUD mon hoc.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/classes", "CRUD lop hoc.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/class-members", "CRUD thanh vien lop.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/questions", "CRUD cau hoi.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/question-options", "CRUD dap an.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/exams", "CRUD de thi.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/exam-questions", "CRUD cau hoi trong de.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/exam-attempts", "CRUD luot lam bai.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/attempt-answers", "CRUD cau tra loi.", true),
                            Step("GET/POST/PUT/DELETE", "/api/admin-data/anti-cheat-events", "CRUD canh bao gian lan.", true)
                        ]
                    },
                    new ApiTestGroup
                    {
                        Name = "Feature API Checks",
                        Purpose = "Chi test nhung phan trong 8 chuc nang phu hop voi API.",
                        Steps =
                        [
                            Step("GET", "/api/features/status", "Danh dau chuc nang nao test bang Swagger duoc.", false),
                            Step("GET", "/api/features/search-suggestions?q=...", "Test autocomplete Meilisearch/fallback SQL.", true),
                            Step("GET", "/api/features/signalr-status", "Kiem tra hub URL va client JS, khong thay the websocket test.", false),
                            Step("POST", "/api/features/anti-cheat/report", "Ghi thu mot anti-cheat event cho attempt co san.", true),
                            Step("WEB", "/Identity/Login/Account/Login", "Google OAuth gop voi login/register, test tren web UI neu can.", false),
                            Step("WEB", "Language selector", "Da ngon ngu la UI/cookie, khong dua vao checklist API.", false),
                            Step("SKIP", "Deploy", "Deploy khong thuoc test API localhost.", false)
                        ]
                    }
                ]
            });
        }

        [HttpGet("status")]
        [AllowAnonymous]
        public async Task<ActionResult<FeatureStatusResponse>> Status(CancellationToken cancellationToken)
        {
            var searchHealth = await _meilisearch.GetHealthAsync(cancellationToken);
            var signalRClientPath = Path.Combine(_environment.WebRootPath, "vendor", "signalr", "signalr.min.js");
            var warningCount = await _context.AntiCheatEvents.CountAsync(cancellationToken);

            return Ok(new FeatureStatusResponse
            {
                Items =
                [
                    Feature("Google OAuth 2.0", false, HasValues("Authentication:Google:ClientId", "Authentication:Google:ClientSecret") ? "Configured" : "Needs config", null, "OAuth is a browser redirect flow; test with the login/register pages."),
                    Feature("OTP Gmail dang ky", true, HasEmailProvider() || _environment.IsDevelopment() ? "API test ready" : "Needs email provider", "/api/auth/register", "Localhost Development returns developmentCode for Swagger testing."),
                    Feature("OTP quen mat khau", true, HasEmailProvider() || _environment.IsDevelopment() ? "API test ready" : "Needs email provider", "/api/auth/forgot-password", "Localhost Development returns developmentCode for Swagger testing."),
                    Feature("JWT & Refresh Token", true, HasValues("Jwt:Key") ? "Ready" : "Needs Jwt:Key", "/api/auth/login", null),
                    Feature("Meilisearch AutoComplete", true, searchHealth.Reachable ? "Ready" : "Fallback SQL", "/api/features/search-suggestions", searchHealth.Message),
                    Feature("SignalR chat socket", false, System.IO.File.Exists(signalRClientPath) ? "Configured" : "Missing client JS", "/api/features/signalr-status", "Swagger can only check status. Use browser/websocket for real chat."),
                    Feature("Chong gian lan", true, $"Ready - {warningCount} saved events", "/api/features/anti-cheat/report", null),
                    Feature("Chuyen doi ngon ngu", false, "UI only", null, "Localization is tested on Razor UI/cookie, not as a CRUD API.")
                ]
            });
        }

        [HttpGet("search-suggestions")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<IReadOnlyList<SearchSuggestionApiItem>>> SearchSuggestions(string? q, CancellationToken cancellationToken)
        {
            var term = q?.Trim();
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(Array.Empty<SearchSuggestionApiItem>());
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
                    item.Code
                })
                .ToListAsync(cancellationToken);

            var documents = classes.Select(item => new SearchIndexDocument(
                    $"class-{item.Id}", "class", item.Id, item.Name, $"{item.SubjectName} - {item.Code}"))
                .Concat(exams.Select(item => new SearchIndexDocument(
                    $"exam-{item.Id}", "exam", item.Id, item.Title, $"{item.ClassName} - {item.Code}")))
                .ToList();

            var hits = await _meilisearch.SearchAsync(term, documents, cancellationToken);
            var result = hits?.Select(ToDto)
                ?? documents
                    .Where(item => item.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                        || item.Meta.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                    .Take(8)
                    .Select(ToDto);

            return Ok(result.ToList());
        }

        [HttpGet("signalr-status")]
        [AllowAnonymous]
        public ActionResult<SignalRStatusResponse> SignalRStatus()
        {
            var signalRClientPath = Path.Combine(_environment.WebRootPath, "vendor", "signalr", "signalr.min.js");
            return Ok(new SignalRStatusResponse
            {
                BrowserClientExists = System.IO.File.Exists(signalRClientPath),
                TestNote = "Swagger cannot keep a SignalR websocket session. Open /Chat/Room?type=class&id={classId} in the browser for the real chat test."
            });
        }

        [HttpPost("anti-cheat/report")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        public async Task<IActionResult> ReportAntiCheat(FeatureAntiCheatReportRequest request, CancellationToken cancellationToken)
        {
            var attemptExists = await _context.ExamAttempts
                .AnyAsync(attempt => attempt.Id == request.ExamAttemptId, cancellationToken);
            if (!attemptExists)
            {
                return NotFound(new { message = "Exam attempt not found." });
            }

            var count = await _context.AntiCheatEvents
                .CountAsync(item => item.ExamAttemptId == request.ExamAttemptId, cancellationToken) + 1;
            var item = new AntiCheatEvent
            {
                ExamAttemptId = request.ExamAttemptId,
                EventType = request.EventType,
                Severity = count >= 4 ? AntiCheatSeverity.High : count == 3 ? AntiCheatSeverity.Medium : AntiCheatSeverity.Low,
                Description = request.Note,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    Source = "Swagger API test",
                    Count = count,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                }),
                OccurredAt = DateTime.UtcNow
            };

            _context.AntiCheatEvents.Add(item);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                item.Id,
                item.ExamAttemptId,
                item.EventType,
                item.Severity,
                ViolationCount = count,
                item.OccurredAt
            });
        }

        private static ApiTestStep Step(string method, string path, string testWhat, bool needsBearerToken, string? note = null)
        {
            return new ApiTestStep
            {
                Method = method,
                Path = path,
                TestWhat = testWhat,
                NeedsBearerToken = needsBearerToken,
                Note = note
            };
        }

        private static FeatureStatusItem Feature(string name, bool apiTestable, string status, string? endpoint, string? note)
        {
            return new FeatureStatusItem
            {
                Name = name,
                ApiTestable = apiTestable,
                Status = status,
                Endpoint = endpoint,
                Note = note
            };
        }

        private static SearchSuggestionApiItem ToDto(SearchIndexDocument item)
        {
            return new SearchSuggestionApiItem
            {
                Type = item.Type,
                Id = item.EntityId,
                Title = item.Title,
                Meta = item.Meta
            };
        }

        private static SearchSuggestionApiItem ToDto(SearchIndexHit item)
        {
            return new SearchSuggestionApiItem
            {
                Type = item.Type,
                Id = item.EntityId,
                Title = item.Title,
                Meta = item.Meta
            };
        }

        private bool HasValues(params string[] keys)
        {
            return keys.All(key => !string.IsNullOrWhiteSpace(_configuration[key]));
        }

        private bool HasEmailProvider()
        {
            return EmailConfigurationHelper.HasEmailProvider(_configuration);
        }
    }
}
