namespace LT_Web_Nhom4.Areas.Admin.Models
{
    public class AdminTechnologyViewModel
    {
        public IList<AdminTechnologyItemViewModel> Items { get; set; } = new List<AdminTechnologyItemViewModel>();

        public int ReadyCount => Items.Count(item => item.State == TechnologyState.Ready);
    }

    public class AdminTechnologyItemViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Icon { get; set; } = "ri-code-line";

        public TechnologyState State { get; set; }

        public string? ActionUrl { get; set; }

        public string? ActionLabel { get; set; }
    }

    public enum TechnologyState
    {
        Ready,
        Fallback,
        NeedsConfiguration
    }
}
