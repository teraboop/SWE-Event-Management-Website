using EventManagement.Models.Events;
using EventManagement.Models.Profile;
using Microsoft.AspNetCore.Identity;

namespace EventManagement.Data;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public ICollection<UserInterest> Interests { get; set; } = [];

    public ICollection<SavedEvent> SavedEvents { get; set; } = [];
}