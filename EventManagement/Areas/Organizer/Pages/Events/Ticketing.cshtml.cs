using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Organizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

[Authorize(Roles = "Organizer,Admin")]
public class TicketingModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketingModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    // The page hosts three independent forms. Each handler binds only the form
    // that was submitted (via TryUpdateModelAsync) and reloads the other
    // sections from the database, so a failed submit never resets the others.
    public TicketingSettingsInputModel Input { get; set; } = new();

    public TicketTypeEditInputModel TicketEdit { get; set; } = new();

    public NewTicketTypeInputModel NewTicketType { get; set; } = new();

    public string EventTitle { get; private set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;
        LoadSettings(eventEntity);
        await LoadTicketTypesAsync(eventEntity.Id);

        return Page();
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .Include(@event => @event.TicketTypes)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;
        await TryUpdateModelAsync(Input, nameof(Input));

        if (!Enum.IsDefined(Input.TicketSalesMode))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.TicketSalesMode)}",
                "Select a valid ticket sales option.");
        }

        if (Input.TicketSalesMode == TicketSalesMode.External)
        {
            if (string.IsNullOrWhiteSpace(Input.ExternalTicketingUrl))
            {
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.ExternalTicketingUrl)}",
                    "Enter the external ticketing link.");
            }
            else if (!Uri.TryCreate(
                         Input.ExternalTicketingUrl.Trim(),
                         UriKind.Absolute,
                         out var ticketingUri)
                     || (ticketingUri.Scheme != Uri.UriSchemeHttps
                         && ticketingUri.Scheme != Uri.UriSchemeHttp))
            {
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.ExternalTicketingUrl)}",
                    "Enter a valid link beginning with http:// or https://.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadTicketTypesAsync(eventEntity.Id);
            return Page();
        }

        eventEntity.TicketSalesMode = Input.TicketSalesMode;
        eventEntity.ExternalTicketingUrl =
            Input.TicketSalesMode == TicketSalesMode.External
                ? Input.ExternalTicketingUrl!.Trim()
                : null;
        SyncMinimumPrice(eventEntity);

        await _dbContext.SaveChangesAsync();

        StatusMessage = "Ticket settings saved.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateTicketTypesAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .Include(@event => @event.TicketTypes)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;
        await TryUpdateModelAsync(TicketEdit, nameof(TicketEdit));
        ValidateExistingTicketTypes(eventEntity);

        if (!ModelState.IsValid)
        {
            LoadSettings(eventEntity);
            return Page();
        }

        foreach (var ticketTypeInput in TicketEdit.TicketTypes)
        {
            var existingTicketType = eventEntity.TicketTypes
                .Single(ticketType => ticketType.Id == ticketTypeInput.Id);

            existingTicketType.Name = ticketTypeInput.Name.Trim();
            existingTicketType.Price = ticketTypeInput.Price;
            existingTicketType.QuantityAvailable = ticketTypeInput.QuantityAvailable;
            existingTicketType.SalesStartAt = ticketTypeInput.SalesStartAt;
            existingTicketType.SalesEndAt = ticketTypeInput.SalesEndAt;
            existingTicketType.IsEnabled = ticketTypeInput.IsEnabled;
        }

        SyncMinimumPrice(eventEntity);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Most likely a purchase finished between validation and saving,
            // leaving a new capacity below the number sold. The database
            // constraints rejected the whole save, so nothing was changed.
            ModelState.AddModelError(
                nameof(TicketEdit),
                "Ticket data changed while you were editing (for example, tickets were sold), so nothing was saved. Check the Sold column and try again.");
            await RefreshQuantitySoldAsync();
            LoadSettings(eventEntity);
            return Page();
        }

        StatusMessage = "Ticket types updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddTicketTypeAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .Include(@event => @event.TicketTypes)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;
        await TryUpdateModelAsync(NewTicketType, nameof(NewTicketType));

        if (eventEntity.TicketSalesMode != TicketSalesMode.OnPlatform)
        {
            ModelState.AddModelError(
                nameof(NewTicketType),
                "Select on-platform ticket sales and save the settings before adding ticket types.");
        }

        ValidateTicketType(
            NewTicketType.Name,
            NewTicketType.Price,
            NewTicketType.QuantityAvailable,
            NewTicketType.SalesStartAt,
            NewTicketType.SalesEndAt,
            nameof(NewTicketType));

        var newName = NewTicketType.Name?.Trim() ?? string.Empty;
        if (newName.Length > 0 &&
            eventEntity.TicketTypes.Any(ticketType =>
                string.Equals(ticketType.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(
                $"{nameof(NewTicketType)}.{nameof(NewTicketType.Name)}",
                "Ticket type names must be unique for this event.");
        }

        if (!ModelState.IsValid)
        {
            LoadSettings(eventEntity);
            await LoadTicketTypesAsync(eventEntity.Id);
            return Page();
        }

        eventEntity.TicketTypes.Add(new EventTicketType
        {
            Name = newName,
            Price = NewTicketType.Price,
            QuantityAvailable = NewTicketType.QuantityAvailable,
            SalesStartAt = NewTicketType.SalesStartAt,
            SalesEndAt = NewTicketType.SalesEndAt,
            IsEnabled = true
        });
        SyncMinimumPrice(eventEntity);

        await _dbContext.SaveChangesAsync();
        StatusMessage = "Ticket type added.";
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// Form-level (not field-specific) errors for one form, keyed by that
    /// form's prefix, so each form's summary only shows its own errors.
    /// </summary>
    public IReadOnlyList<string> GetFormErrors(string formKey) =>
        ModelState.TryGetValue(formKey, out var entry)
            ? entry.Errors.Select(error => error.ErrorMessage).ToList()
            : [];

    private bool CanManage(Event eventEntity)
    {
        var userId = _userManager.GetUserId(User);

        return User.IsInRole("Admin") ||
            eventEntity.OrganizerId == userId;
    }

    private void LoadSettings(Event eventEntity)
    {
        Input = new TicketingSettingsInputModel
        {
            TicketSalesMode = eventEntity.TicketSalesMode,
            ExternalTicketingUrl = eventEntity.ExternalTicketingUrl
        };
    }

    private async Task LoadTicketTypesAsync(int eventId)
    {
        TicketEdit.TicketTypes = await _dbContext.EventTicketTypes
            .AsNoTracking()
            .Where(ticketType => ticketType.EventId == eventId)
            .OrderBy(ticketType => ticketType.Name)
            .Select(ticketType => new TicketTypeInputModel
            {
                Id = ticketType.Id,
                Name = ticketType.Name,
                Price = ticketType.Price,
                QuantityAvailable = ticketType.QuantityAvailable,
                SalesStartAt = ticketType.SalesStartAt,
                SalesEndAt = ticketType.SalesEndAt,
                IsEnabled = ticketType.IsEnabled,
                QuantitySold = ticketType.QuantitySold
            })
            .ToListAsync();
    }

    /// <summary>
    /// Keep the organizer's edits on screen but show the current sold counts
    /// after a save was rejected for capacity.
    /// </summary>
    private async Task RefreshQuantitySoldAsync()
    {
        var ids = TicketEdit.TicketTypes
            .Where(row => row.Id.HasValue)
            .Select(row => row.Id!.Value)
            .ToList();

        var soldById = await _dbContext.EventTicketTypes
            .AsNoTracking()
            .Where(ticketType => ids.Contains(ticketType.Id))
            .ToDictionaryAsync(ticketType => ticketType.Id, ticketType => ticketType.QuantitySold);

        foreach (var row in TicketEdit.TicketTypes)
        {
            if (row.Id is int rowId && soldById.TryGetValue(rowId, out var sold))
            {
                row.QuantitySold = sold;
            }
        }
    }

    /// <summary>
    /// Event.MinimumPrice drives the browse page's price filter and the
    /// "From $X" labels. Keep it in step with the ticket types, so organizers
    /// don't have to maintain it separately and it can't disagree with the
    /// ticket table.
    /// </summary>
    private static void SyncMinimumPrice(Event eventEntity)
    {
        switch (eventEntity.TicketSalesMode)
        {
            case TicketSalesMode.NotRequired:
                eventEntity.MinimumPrice = 0;
                break;

            case TicketSalesMode.OnPlatform:
                var enabledPrices = eventEntity.TicketTypes
                    .Where(ticketType => ticketType.IsEnabled)
                    .Select(ticketType => ticketType.Price)
                    .ToList();

                if (enabledPrices.Count > 0)
                {
                    eventEntity.MinimumPrice = enabledPrices.Min();
                }
                break;

            // External or not configured: prices live elsewhere, so keep the
            // existing value.
        }
    }

    private void ValidateExistingTicketTypes(Event eventEntity)
    {
        var submittedIds = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < TicketEdit.TicketTypes.Count; index++)
        {
            var ticketTypeInput = TicketEdit.TicketTypes[index];
            var rowPrefix = $"{nameof(TicketEdit)}.{nameof(TicketEdit.TicketTypes)}[{index}]";

            if (ticketTypeInput.Id is not int ticketTypeId ||
                !submittedIds.Add(ticketTypeId) ||
                eventEntity.TicketTypes.All(ticketType => ticketType.Id != ticketTypeId))
            {
                ModelState.AddModelError(
                    nameof(TicketEdit),
                    "A submitted ticket type is invalid or does not belong to this event.");
                continue;
            }

            // Sold counts come from the database, never from the form.
            var existingTicketType = eventEntity.TicketTypes.Single(ticketType => ticketType.Id == ticketTypeId);
            ticketTypeInput.QuantitySold = existingTicketType.QuantitySold;

            if (ticketTypeInput.QuantityAvailable < existingTicketType.QuantitySold)
            {
                ModelState.AddModelError(
                    $"{rowPrefix}.{nameof(TicketTypeInputModel.QuantityAvailable)}",
                    $"Capacity can't be lower than the {existingTicketType.QuantitySold} tickets already sold.");
            }

            var trimmedName = ticketTypeInput.Name?.Trim() ?? string.Empty;
            if (trimmedName.Length > 0 && !names.Add(trimmedName))
            {
                ModelState.AddModelError(
                    $"{rowPrefix}.{nameof(TicketTypeInputModel.Name)}",
                    "Ticket type names must be unique for this event.");
            }

            ValidateTicketType(
                ticketTypeInput.Name,
                ticketTypeInput.Price,
                ticketTypeInput.QuantityAvailable,
                ticketTypeInput.SalesStartAt,
                ticketTypeInput.SalesEndAt,
                rowPrefix);
        }

        foreach (var uneditedTicketType in eventEntity.TicketTypes.Where(ticketType => !submittedIds.Contains(ticketType.Id)))
        {
            if (!names.Add(uneditedTicketType.Name))
            {
                ModelState.AddModelError(
                    nameof(TicketEdit),
                    $"Ticket type names must be unique for this event (\"{uneditedTicketType.Name}\" is already used).");
            }
        }
    }

    private void ValidateTicketType(
        string? name,
        decimal price,
        int quantityAvailable,
        DateTimeOffset salesStartAt,
        DateTimeOffset salesEndAt,
        string modelPrefix)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            ModelState.AddModelError($"{modelPrefix}.Name", "Enter a name of 1 to 100 characters.");
        }

        if (price < 0 || price > 10000000)
        {
            ModelState.AddModelError($"{modelPrefix}.Price", "Price must be between 0 and 10,000,000.");
        }

        if (quantityAvailable < 0)
        {
            ModelState.AddModelError($"{modelPrefix}.QuantityAvailable", "Quantity cannot be negative.");
        }

        if (salesEndAt <= salesStartAt)
        {
            ModelState.AddModelError($"{modelPrefix}.SalesEndAt", "The sales end must be after the sales start.");
        }
    }
}
