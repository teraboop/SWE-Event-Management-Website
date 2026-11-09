using EventManagement.Models.Commerce;

namespace EventManagement.ViewModels.Orders;

/// <summary>One row on the My orders page.</summary>
public class OrderSummaryViewModel
{
    public int Id { get; set; }

    public OrderStatus Status { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public int TicketCount { get; set; }

    public int ItemCount { get; set; }
}

/// <summary>The receipt for one order: its tickets grouped by event, and its merchandise.</summary>
public class OrderDetailsViewModel
{
    public int Id { get; set; }

    public OrderStatus Status { get; set; }

    public DateTimeOffset PlacedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public IReadOnlyList<OrderTicketLineViewModel> TicketLines { get; set; } = [];

    public IReadOnlyList<OrderItemLineViewModel> ItemLines { get; set; } = [];
}

/// <summary>All tickets of one type on an order, collapsed into one line.</summary>
public class OrderTicketLineViewModel
{
    public int EventId { get; set; }

    public string EventTitle { get; set; } = string.Empty;

    public DateTimeOffset EventStartsAt { get; set; }

    public string TicketTypeName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public class OrderItemLineViewModel
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
