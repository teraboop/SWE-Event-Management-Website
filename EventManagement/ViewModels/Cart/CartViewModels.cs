namespace EventManagement.ViewModels.Cart;

/// <summary>One cart line, with today's price and anything that would stop it being bought.</summary>
public class CartLineViewModel
{
    public int Id { get; set; }

    public bool IsTicket { get; set; }

    /// <summary>The ticket type's name, or the product's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>For tickets, the event's title; for products, the vendor's name.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>For tickets, when the event starts.</summary>
    public DateTimeOffset? EventStartsAt { get; set; }

    /// <summary>The event (for tickets) or product page to link to.</summary>
    public int LinkId { get; set; }

    public int? ImageId { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

    /// <summary>The most this line can be raised to right now.</summary>
    public int MaxQuantity { get; set; }

    /// <summary>Why this line can't be bought as it stands, or null when it can.</summary>
    public string? Problem { get; set; }
}

public class CartViewModel
{
    public IReadOnlyList<CartLineViewModel> Lines { get; set; } = [];

    public IEnumerable<CartLineViewModel> TicketLines => Lines.Where(line => line.IsTicket);

    public IEnumerable<CartLineViewModel> ProductLines => Lines.Where(line => !line.IsTicket);

    public decimal Total => Lines.Sum(line => line.LineTotal);

    public int ItemCount => Lines.Sum(line => line.Quantity);

    public bool IsEmpty => Lines.Count == 0;

    public bool HasProblems => Lines.Any(line => line.Problem is not null);

    public bool CanCheckout => !IsEmpty && !HasProblems;
}
