namespace EventManagement.ViewModels.Shop;

public enum ShopSort
{
    Newest = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
    Name = 3
}

/// <summary>The shop's search box and filters, bound from the query string.</summary>
public class ShopFilterViewModel
{
    /// <summary>Matches product names, descriptions and vendor names.</summary>
    public string? Search { get; set; }

    public int? VendorId { get; set; }

    public decimal? MaximumPrice { get; set; }

    public bool InStockOnly { get; set; }

    public ShopSort Sort { get; set; } = ShopSort.Newest;

    public int Page { get; set; } = 1;
}

/// <summary>A product as shown in a grid of cards.</summary>
public class ProductCardViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int QuantityInStock { get; set; }

    public bool IsInStock => QuantityInStock > 0;

    public int? ImageId { get; set; }

    public int VendorId { get; set; }

    public string VendorName { get; set; } = string.Empty;
}

public record ShopVendorOption(int Id, string Name);

public class ShopViewModel
{
    public ShopFilterViewModel Filter { get; set; } = new();

    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = [];

    public IReadOnlyList<ShopVendorOption> Vendors { get; set; } = [];

    public int TotalCount { get; set; }

    public int PageCount { get; set; }
}

public class ProductDetailsViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int QuantityInStock { get; set; }

    public bool IsInStock => QuantityInStock > 0;

    public int? ImageId { get; set; }

    public int VendorId { get; set; }

    public string VendorName { get; set; } = string.Empty;

    public string VendorProductsOffered { get; set; } = string.Empty;

    /// <summary>True when the signed-in user is the vendor selling this.</summary>
    public bool IsOwnProduct { get; set; }

    public IReadOnlyList<ProductCardViewModel> MoreFromVendor { get; set; } = [];
}

/// <summary>A vendor's public page: who they are and everything they're selling.</summary>
public class StorefrontViewModel
{
    public int VendorId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string ProductsOffered { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? WebsiteUrl { get; set; }

    public bool IsOwnStore { get; set; }

    public IReadOnlyList<ProductCardViewModel> Products { get; set; } = [];

    /// <summary>Upcoming events this vendor is approved to sell at.</summary>
    public IReadOnlyList<StorefrontEventViewModel> UpcomingEvents { get; set; } = [];
}

public record StorefrontEventViewModel(int Id, string Title, DateTimeOffset StartsAt, string City);

/// <summary>An approved vendor, listed on the page of an event they're selling at.</summary>
public class EventVendorViewModel
{
    public int VendorId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string ProductsOffered { get; set; } = string.Empty;

    /// <summary>The vendor sells online too, so their storefront can be linked.</summary>
    public bool HasStorefront { get; set; }
}
