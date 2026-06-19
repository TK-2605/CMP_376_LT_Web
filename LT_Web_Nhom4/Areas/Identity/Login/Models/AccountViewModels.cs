using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;

namespace LT_Web_Nhom4.Areas.Identity.Login.Models
{
    public class LoginViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mat khau")]
        [Required(ErrorMessage = "Vui long nhap mat khau.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ghi nho toi")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();
    }

    public class RegisterViewModel
    {
        [Display(Name = "Ho va ten")]
        [Required(ErrorMessage = "Vui long nhap ho va ten.")]
        [StringLength(150, ErrorMessage = "Ho va ten khong duoc vuot qua 150 ky tu.")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Ma sinh vien")]
        [StringLength(50, ErrorMessage = "Ma sinh vien khong duoc vuot qua 50 ky tu.")]
        public string? StudentCode { get; set; }

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Mat khau")]
        [Required(ErrorMessage = "Vui long nhap mat khau.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mat khau phai tu 6 den 100 ky tu.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Nhap lai mat khau")]
        [Required(ErrorMessage = "Vui long nhap lai mat khau.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Mat khau nhap lai khong khop.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        public string Email { get; set; } = string.Empty;
    }

    public class ExternalLoginConfirmationViewModel
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Vui long nhap email.")]
        [EmailAddress(ErrorMessage = "Email khong hop le.")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Ho va ten")]
        [Required(ErrorMessage = "Vui long nhap ho va ten.")]
        [StringLength(150, ErrorMessage = "Ho va ten khong duoc vuot qua 150 ky tu.")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Ma sinh vien")]
        [StringLength(50, ErrorMessage = "Ma sinh vien khong duoc vuot qua 50 ky tu.")]
        public string? StudentCode { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
