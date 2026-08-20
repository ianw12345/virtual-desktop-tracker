namespace VirtualDesktopHelper.Models
{
    /// <summary>Clockify workspace returned by the v1 API.</summary>
    public sealed class ClockifyWorkspace
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    /// <summary>Clockify project returned by the v1 API.</summary>
    public sealed class ClockifyProject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Archived { get; set; }
        public ClockifyClient? Client { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(Client?.Name) ? Name : $"{Client.Name} – {Name}";
        public override string ToString() => DisplayName;
    }

    public sealed class ClockifyClient
    {
        public string Name { get; set; } = "";
    }
}
