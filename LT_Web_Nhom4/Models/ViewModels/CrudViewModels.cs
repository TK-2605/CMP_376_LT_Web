namespace LT_Web_Nhom4.Models.ViewModels
{
    public class CrudIndexViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ControllerName { get; set; } = string.Empty;

        public string? AreaName { get; set; }

        public IReadOnlyList<CrudFieldViewModel> Fields { get; set; } = new List<CrudFieldViewModel>();

        public IReadOnlyList<CrudRowViewModel> Rows { get; set; } = new List<CrudRowViewModel>();
    }

    public class CrudRowViewModel
    {
        public string Key { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, string?> Values { get; set; } = new Dictionary<string, string?>();
    }

    public class CrudFormViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ActionName { get; set; } = string.Empty;

        public string ControllerName { get; set; } = string.Empty;

        public string? AreaName { get; set; }

        public string? Key { get; set; }

        public IReadOnlyList<CrudFieldViewModel> Fields { get; set; } = new List<CrudFieldViewModel>();
    }

    public class CrudDetailsViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ControllerName { get; set; } = string.Empty;

        public string? AreaName { get; set; }

        public string Key { get; set; } = string.Empty;

        public IReadOnlyList<CrudFieldViewModel> Fields { get; set; } = new List<CrudFieldViewModel>();
    }

    public class CrudDeleteViewModel : CrudDetailsViewModel
    {
    }

    public class CrudFieldViewModel
    {
        public string Name { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Value { get; set; }

        public string InputType { get; set; } = "text";

        public bool IsKey { get; set; }

        public bool IsReadOnly { get; set; }

        public bool IsNullable { get; set; }

        public bool IsBoolean { get; set; }

        public bool ShowInList { get; set; } = true;

        public bool ShowInForm { get; set; } = true;

        public bool ShowInDetails { get; set; } = true;

        public string? Placeholder { get; set; }

        public string? HelpText { get; set; }

        public IReadOnlyList<CrudOptionViewModel> Options { get; set; } = new List<CrudOptionViewModel>();
    }

    public class CrudOptionViewModel
    {
        public string Value { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}
