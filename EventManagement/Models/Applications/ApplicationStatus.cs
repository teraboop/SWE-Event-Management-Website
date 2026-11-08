namespace EventManagement.Models.Applications;

/// <summary>
/// Lifecycle shared by every kind of application (vendor applications now,
/// panel applications later). The only allowed transitions are out of
/// Submitted:
/// <list type="bullet">
/// <item>Submitted → Approved or Rejected (by a reviewer)</item>
/// <item>Submitted → Withdrawn (by the applicant)</item>
/// </list>
/// Approved, Rejected and Withdrawn are final. To try again, an applicant
/// submits a new application, so the full history is kept.
/// </summary>
public enum ApplicationStatus
{
    Submitted = 0,
    Approved = 1,
    Rejected = 2,
    Withdrawn = 3
}
