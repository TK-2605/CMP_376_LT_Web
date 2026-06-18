using System.ComponentModel.DataAnnotations;

namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminUserSummaryViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string? StudentCode { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public IList<string> Roles { get; set; } = new List<string>();
    }

    public class AdminUserEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? StudentCode { get; set; }

        public bool IsActive { get; set; } = true;

        public IList<string> SelectedRoles { get; set; } = new List<string>();
    }

    public class AdminRoleAssignmentViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public IList<string> AvailableRoles { get; set; } = new List<string>();

        public IList<string> SelectedRoles { get; set; } = new List<string>();
    }
}
