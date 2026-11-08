using EventManagement.Models.Events;
using EventManagement.Models.Profile;

namespace EventManagement.ViewModels.Events;

/// <summary>
/// What the shared listing form needs besides the input itself: the choices
/// for its lists, and the current background when editing an existing event.
/// </summary>
public sealed record EventListingFormOptions(
    IReadOnlyList<EventCategory> Categories,
    IReadOnlyList<InterestCategory> Interests,
    int? CurrentImageId = null);
