namespace EventManagement.ViewModels.Admin;

public class AdminIndexViewModel
{
    public List<AdminUserRowViewModel> Users { get; set; } = [];

    public int PendingOnlineVendorApplications { get; set; }

    public int PendingEventApplications { get; set; }
}

public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public bool IsOrganizer { get; set; }
}
