using LT_Web_Nhom4.Services.Interfaces;

namespace LT_Web_Nhom4.Services.Implementations
{
    public sealed class AppClock : IAppClock
    {
        private readonly TimeZoneInfo _timeZone;

        public AppClock(IConfiguration configuration)
        {
            _timeZone = ResolveTimeZone(configuration["AppTimeZone"]);
        }

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone);

        public DateTime ToAppLocalTime(DateTime utcDateTime)
        {
            var utc = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _timeZone);
        }

        private static TimeZoneInfo ResolveTimeZone(string? configuredId)
        {
            foreach (var id in new[] { configuredId, "Asia/Ho_Chi_Minh", "Asia/Bangkok", "SE Asia Standard Time" })
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone("QuizHubUtcPlus7", TimeSpan.FromHours(7), "QuizHub UTC+7", "QuizHub UTC+7");
        }
    }
}
