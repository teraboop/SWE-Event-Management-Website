using EventManagement.Data;
using EventManagement.Models.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

/// <summary>
/// Checks and stores uploaded images: event backgrounds and product photos. The type is decided from the
/// file's first bytes, not the name or the browser's Content-Type, so only
/// real JPEG, PNG and WebP images are accepted and served.
/// </summary>
public class EventImageService
{
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>For the file input's accept attribute.</summary>
    public const string AcceptAttribute = "image/jpeg,image/png,image/webp";

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EventImageService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Reads and checks an upload. Returns the image to save (not yet added to
    /// the context), or an error message for the form.
    /// </summary>
    public async Task<(EventImage? Image, string? Error)> ReadUploadAsync(
        IFormFile file,
        string uploadedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return (null, "The image file is empty.");
        }

        if (file.Length > MaxBytes)
        {
            return (null, $"Images can be up to {MaxBytes / (1024 * 1024)} MB.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        var data = buffer.ToArray();

        var contentType = DetectContentType(data);
        if (contentType is null)
        {
            return (null, "Upload a JPEG, PNG or WebP image.");
        }

        return (new EventImage
        {
            ContentType = contentType,
            Data = data,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = _timeProvider.GetUtcNow()
        }, null);
    }

    /// <summary>
    /// Deletes an image nothing points at any more, e.g. after an organizer
    /// replaces their event's background. Images still used by an event, an
    /// application or a product are kept.
    /// </summary>
    public async Task DeleteIfUnusedAsync(int imageId, CancellationToken cancellationToken = default)
    {
        var inUse =
            await _dbContext.Events.AnyAsync(@event => @event.BackgroundImageId == imageId, cancellationToken) ||
            await _dbContext.EventApplications.AnyAsync(application => application.BackgroundImageId == imageId, cancellationToken) ||
            await _dbContext.VendorProducts.AnyAsync(product => product.ImageId == imageId, cancellationToken);

        if (!inUse)
        {
            await _dbContext.EventImages
                .Where(image => image.Id == imageId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        // RIFF....WEBP
        if (data.Length >= 12 &&
            data[..4].SequenceEqual("RIFF"u8) &&
            data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
