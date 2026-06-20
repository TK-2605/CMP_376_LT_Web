namespace LT_Web_Nhom4.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);

        Task SendOtpEmailAsync(string toEmail, string otp, string purpose);
    }
}
