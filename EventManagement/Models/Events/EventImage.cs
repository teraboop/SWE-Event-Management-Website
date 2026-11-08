namespace EventManagement.Models.Events;

/// <summary>
/// An uploaded image (an event's page background). Stored in the database
/// rather than on disk so every copy of the app that shares the database can
/// serve it; served by ImagesController.
/// </summary>
public class EventImage
{
    public int Id { get; set; }

    /// <summary>image/jpeg, image/png or image/webp; checked against the file's bytes on upload.</summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
