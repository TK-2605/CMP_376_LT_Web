using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace LT_Web_Nhom4.Controllers
{
    public class LocalizationController : Controller
    {
        private static readonly HashSet<string> SupportedCultures =
            new(StringComparer.OrdinalIgnoreCase) { "vi", "en", "ja", "ko", "zh" };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetLanguage(string culture, string? returnUrl)
        {
            if (!SupportedCultures.Contains(culture))
            {
                culture = "vi";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps
                });

            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
        }
    }
}
