using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EventManagement.ViewModels.Events;

/// <summary>
/// The fields that make up an event listing. Used by the event application
/// form and by the organizer's Edit listing page, so both ask for the same
/// things with the same rules.
/// </summary>
public class EventListingInputModel : IValidatableObject
{
    [Required(ErrorMessage = "Enter the event's name.")]
    [StringLength(200)]
    [Display(Name = "Event name")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe the event.")]
    [StringLength(4000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int? EventCategoryId { get; set; }

    /// <summary>Bound from a datetime-local input, so it has no offset; read as the server's local time like the ticketing forms.</summary>
    [Required(ErrorMessage = "Enter when the event starts.")]
    [Display(Name = "Starts")]
    public DateTime? StartsAt { get; set; }

    [Required(ErrorMessage = "Enter when the event ends.")]
    [Display(Name = "Ends")]
    public DateTime? EndsAt { get; set; }

    [Required(ErrorMessage = "Enter the venue.")]
    [StringLength(200)]
    [Display(Name = "Venue")]
    public string VenueName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the city.")]
    [StringLength(100)]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Postal code (optional)")]
    public string? PostalCode { get; set; }

    [Url(ErrorMessage = "Enter a full web address, starting with https://.")]
    [StringLength(2048)]
    [Display(Name = "Event website (optional)")]
    public string? ExternalWebsiteUrl { get; set; }

    [Display(Name = "Interests")]
    public List<int> InterestIds { get; set; } = [];

    [Display(Name = "Background image (optional)")]
    public IFormFile? BackgroundImage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt is not null && EndsAt is not null && EndsAt <= StartsAt)
        {
            yield return new ValidationResult(
                "The event must end after it starts.",
                [nameof(EndsAt)]);
        }
    }

    public DateTimeOffset StartsAtOffset => new(StartsAt!.Value);

    public DateTimeOffset EndsAtOffset => new(EndsAt!.Value);
}

/// <summary>An event listing plus how to reach the person applying to run it.</summary>
public class EventApplicationInputModel : EventListingInputModel
{
    [Required(ErrorMessage = "Enter a contact name.")]
    [StringLength(150)]
    [Display(Name = "Contact name")]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a contact email.")]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [Phone]
    [StringLength(30)]
    [Display(Name = "Contact phone (optional)")]
    public string? ContactPhone { get; set; }

    [StringLength(150)]
    [Display(Name = "Organization (optional)")]
    public string? OrganizationName { get; set; }

    [StringLength(1000)]
    [Display(Name = "Note to the reviewer (optional)")]
    public string? Message { get; set; }
}
