namespace LT_Web_Nhom4.Services.Interfaces
{
    public interface IAppClock
    {
        DateTime UtcNow { get; }

        DateTime Now { get; }

        DateTime ToAppLocalTime(DateTime utcDateTime);
    }
}
