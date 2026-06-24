using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;

namespace LT_Web_Nhom4.Areas.Identity.Login.Models
{
    public class LoginViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mật khẩu")]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nhớ tôi")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public bool GoogleOAuthConfigured { get; set; }

        public bool ShowGoogleOAuthHint { get; set; }
    }

    public class RegisterViewModel
    {
        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Mã sinh viên")]
        [StringLength(50, ErrorMessage = "Mã sinh viên không được vượt quá 50 ký tự.")]
        public string? StudentCode { get; set; }

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mật khẩu")]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Nhập lại mật khẩu")]
        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public bool GoogleOAuthConfigured { get; set; }

        public bool ShowGoogleOAuthHint { get; set; }

        public bool SmtpConfigured { get; set; }

        public string? EmailProviderProblem { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        public bool SmtpConfigured { get; set; }

        public string? EmailProviderProblem { get; set; }
    }

    public class ConfirmRegistrationViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mã xác nhận")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác nhận gồm 6 chữ số.")]
        public string? Code { get; set; }

        public string? Token { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Display(Name = "Mật khẩu mới")]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Nhập lại mật khẩu mới")]
        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordOtpViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mã OTP")]
        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP gồm đúng 6 chữ số.")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "Mật khẩu mới")]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Nhập lại mật khẩu mới")]
        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ExternalLoginConfirmationViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Họ và tên")]
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Mã sinh viên")]
        [StringLength(50, ErrorMessage = "Mã sinh viên không được vượt quá 50 ký tự.")]
        public string? StudentCode { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
