using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;
using EventManagement.Models.Vendors;
using EventManagement.ViewModels.Shop;
using EventManagement.ViewModels.Vendors;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

/// <param name="Field">The form field the message belongs to, when it's about one field.</param>
public sealed record VendorProductResult(
    bool Succeeded,
    string Message,
    int? ProductId = null,
    string? Field = null);

/// <summary>A user's standing as a seller: their vendor profile, if any, and whether they may list products.</summary>
public sealed record SellerStatus(VendorProfile? Profile, bool IsApprovedForOnlineSales)
{
    public bool CanSell => Profile is not null && IsApprovedForOnlineSales;
}

/// <summary>
/// The single owner of the marketplace rules: who may list products, which
/// products shoppers can see, and how vendors change their listings. The
/// future cart should take products from <see cref="AvailableProducts"/> so
/// it sells exactly what the shop shows.
/// </summary>
public class VendorProductService
{
    public const int ShopPageSize = 24;

    public const int MoreFromVendorCount = 4;

    private readonly ApplicationDbContext _dbContext;
    private readonly EventImageService _imageService;
    private readonly TimeProvider _timeProvider;

    public VendorProductService(
        ApplicationDbContext dbContext,
        EventImageService imageService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _imageService = imageService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Products shoppers can see (and later buy): listed by the vendor, not
    /// archived, from a vendor approved for online sales. Out-of-stock
    /// products are included and shown as such. Not marked AsNoTracking, so
    /// it can be nested inside other queries; callers here only project.
    /// </summary>
    public IQueryable<VendorProduct> AvailableProducts() =>
        _dbContext.VendorProducts
            .Where(product =>
                product.IsListed &&
                product.ArchivedAt == null &&
                product.VendorProfile.Applications.Any(application =>
                    application.Kind == VendorApplicationKind.OnlineSales &&
                    application.Status == ApplicationStatus.Approved));

    // ----- Vendor side -------------------------------------------------------

    public async Task<SellerStatus> GetSellerStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.VendorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerUserId == userId, cancellationToken);

        if (profile is null)
        {
            return new SellerStatus(null, false);
        }

        return new SellerStatus(profile, await IsApprovedForOnlineSalesAsync(profile.Id, cancellationToken));
    }

    /// <summary>The vendor's own products, including hidden ones, newest first. Archived ones are left out.</summary>
    public Task<List<VendorProduct>> GetOwnProductsAsync(int vendorProfileId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorProducts
            .AsNoTracking()
            .Where(product => product.VendorProfileId == vendorProfileId && product.ArchivedAt == null)
            .OrderByDescending(product => product.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<VendorProduct?> GetOwnProductAsync(int vendorProfileId, int productId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorProducts
            .AsNoTracking()
            .SingleOrDefaultAsync(product =>
                product.Id == productId &&
                product.VendorProfileId == vendorProfileId &&
                product.ArchivedAt == null,
                cancellationToken);

    /// <summary>
    /// Adds a product (when <paramref name="productId"/> is null) or saves
    /// changes to one of the user's own products, including replacing or
    /// removing its photo. Only vendors approved for online sales may do this.
    /// Field formats are checked by the page's model validation first.
    /// </summary>
    public async Task<VendorProductResult> SaveAsync(
        string userId,
        int? productId,
        VendorProductInputModel input,
        bool removeImage,
        CancellationToken cancellationToken = default)
    {
        var seller = await GetSellerStatusAsync(userId, cancellationToken);
        if (!seller.CanSell)
        {
            return Fail("Only vendors approved to sell online can list products.");
        }

        VendorProduct? product;
        if (productId is int id)
        {
            product = await _dbContext.VendorProducts.SingleOrDefaultAsync(item =>
                item.Id == id &&
                item.VendorProfileId == seller.Profile!.Id &&
                item.ArchivedAt == null,
                cancellationToken);

            if (product is null)
            {
                return Fail("That product doesn't exist or isn't yours.");
            }
        }
        else
        {
            product = new VendorProduct
            {
                VendorProfileId = seller.Profile!.Id,
                Name = string.Empty,
                Description = string.Empty,
                CreatedAt = _timeProvider.GetUtcNow()
            };
            _dbContext.VendorProducts.Add(product);
        }

        EventImage? newImage = null;
        if (input.Image is not null)
        {
            (newImage, var imageError) = await _imageService.ReadUploadAsync(input.Image, userId, cancellationToken);
            if (imageError is not null)
            {
                _dbContext.ChangeTracker.Clear();
                return Fail(imageError, field: nameof(VendorProductInputModel.Image));
            }
        }

        product.Name = input.Name.Trim();
        product.Description = input.Description.Trim();
        product.Price = decimal.Round(input.Price!.Value, 2);
        product.QuantityInStock = input.QuantityInStock!.Value;
        product.IsListed = input.IsListed;
        product.UpdatedAt = _timeProvider.GetUtcNow();

        var previousImageId = product.ImageId;
        if (newImage is not null)
        {
            product.Image = newImage;
        }
        else if (removeImage)
        {
            product.ImageId = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (previousImageId is int oldImageId && product.ImageId != oldImageId)
        {
            await _imageService.DeleteIfUnusedAsync(oldImageId, cancellationToken);
        }

        return new VendorProductResult(
            true,
            productId is null ? $"\"{product.Name}\" added." : $"\"{product.Name}\" saved.",
            product.Id);
    }

    /// <summary>Shows or hides one of the user's products in the shop.</summary>
    public async Task<VendorProductResult> SetListedAsync(
        string userId,
        int productId,
        bool listed,
        CancellationToken cancellationToken = default)
    {
        var updated = await _dbContext.VendorProducts
            .Where(product =>
                product.Id == productId &&
                product.VendorProfile.OwnerUserId == userId &&
                product.ArchivedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(product => product.IsListed, listed)
                .SetProperty(product => product.UpdatedAt, _timeProvider.GetUtcNow()),
                cancellationToken);

        return updated == 1
            ? new VendorProductResult(true, listed ? "Product is now shown in the shop." : "Product hidden from the shop.", productId)
            : Fail("That product doesn't exist or isn't yours.");
    }

    /// <summary>
    /// Removes a product from the shop and the vendor's list. The vendor can
    /// remove their own products; admins can remove any product, e.g. one
    /// that breaks the site's rules. The row is kept for order history.
    /// </summary>
    public async Task<VendorProductResult> ArchiveAsync(
        string userId,
        bool userIsAdmin,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var updated = await _dbContext.VendorProducts
            .Where(product =>
                product.Id == productId &&
                product.ArchivedAt == null &&
                (userIsAdmin || product.VendorProfile.OwnerUserId == userId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(product => product.ArchivedAt, now)
                .SetProperty(product => product.IsListed, false)
                .SetProperty(product => product.UpdatedAt, now),
                cancellationToken);

        return updated == 1
            ? new VendorProductResult(true, "Product removed.", productId)
            : Fail("That product has already been removed, or isn't yours.");
    }

    // ----- Shopper side ------------------------------------------------------

    public async Task<ShopViewModel> GetShopAsync(ShopFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var query = AvailableProducts();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(product =>
                product.Name.Contains(search) ||
                product.Description.Contains(search) ||
                product.VendorProfile.BusinessName.Contains(search));
        }

        if (filter.VendorId is int vendorId)
        {
            query = query.Where(product => product.VendorProfileId == vendorId);
        }

        if (filter.MaximumPrice is decimal maximumPrice)
        {
            query = query.Where(product => product.Price <= maximumPrice);
        }

        if (filter.InStockOnly)
        {
            query = query.Where(product => product.QuantityInStock > 0);
        }

        query = filter.Sort switch
        {
            ShopSort.PriceLowToHigh => query.OrderBy(product => product.Price).ThenBy(product => product.Name),
            ShopSort.PriceHighToLow => query.OrderByDescending(product => product.Price).ThenBy(product => product.Name),
            ShopSort.Name => query.OrderBy(product => product.Name),
            _ => query.OrderByDescending(product => product.CreatedAt).ThenByDescending(product => product.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)ShopPageSize));
        filter.Page = Math.Clamp(filter.Page, 1, pageCount);

        var products = await ToCards(query
                .Skip((filter.Page - 1) * ShopPageSize)
                .Take(ShopPageSize))
            .ToListAsync(cancellationToken);

        // Only vendors with something in the shop are worth filtering by.
        var available = AvailableProducts();
        var vendors = await _dbContext.VendorProfiles
            .AsNoTracking()
            .Where(profile => available.Any(product => product.VendorProfileId == profile.Id))
            .OrderBy(profile => profile.BusinessName)
            .Select(profile => new ShopVendorOption(profile.Id, profile.BusinessName))
            .ToListAsync(cancellationToken);

        return new ShopViewModel
        {
            Filter = filter,
            Products = products,
            Vendors = vendors,
            TotalCount = totalCount,
            PageCount = pageCount
        };
    }

    public async Task<ProductDetailsViewModel?> GetProductAsync(
        int productId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var product = await AvailableProducts()
            .Where(item => item.Id == productId)
            .Select(item => new ProductDetailsViewModel
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                QuantityInStock = item.QuantityInStock,
                ImageId = item.ImageId,
                VendorId = item.VendorProfileId,
                VendorName = item.VendorProfile.BusinessName,
                VendorProductsOffered = item.VendorProfile.ProductsOffered,
                IsOwnProduct = userId != null && item.VendorProfile.OwnerUserId == userId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        product.MoreFromVendor = await ToCards(AvailableProducts()
                .Where(item => item.VendorProfileId == product.VendorId && item.Id != productId)
                .OrderByDescending(item => item.QuantityInStock > 0)
                .ThenByDescending(item => item.CreatedAt)
                .Take(MoreFromVendorCount))
            .ToListAsync(cancellationToken);

        return product;
    }

    /// <summary>A vendor's public storefront. Only vendors approved for online sales have one.</summary>
    public async Task<StorefrontViewModel?> GetStorefrontAsync(
        int vendorProfileId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.VendorProfiles
            .AsNoTracking()
            .Where(item => item.Id == vendorProfileId)
            .Select(item => new StorefrontViewModel
            {
                VendorId = item.Id,
                BusinessName = item.BusinessName,
                ProductsOffered = item.ProductsOffered,
                Description = item.Description,
                WebsiteUrl = item.WebsiteUrl,
                IsOwnStore = userId != null && item.OwnerUserId == userId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null || !await IsApprovedForOnlineSalesAsync(vendorProfileId, cancellationToken))
        {
            return null;
        }

        profile.Products = await ToCards(AvailableProducts()
                .Where(product => product.VendorProfileId == vendorProfileId)
                .OrderByDescending(product => product.QuantityInStock > 0)
                .ThenByDescending(product => product.CreatedAt))
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        profile.UpcomingEvents = await _dbContext.VendorApplications
            .AsNoTracking()
            .Where(application =>
                application.VendorProfileId == vendorProfileId &&
                application.Kind == VendorApplicationKind.Event &&
                application.Status == ApplicationStatus.Approved &&
                application.Event!.Status == EventStatus.Published &&
                application.Event.EndsAt > now)
            .OrderBy(application => application.Event!.StartsAt)
            .Select(application => new StorefrontEventViewModel(
                application.Event!.Id,
                application.Event.Title,
                application.Event.StartsAt,
                application.Event.City))
            .ToListAsync(cancellationToken);

        return profile;
    }

    /// <summary>
    /// Vendors approved to sell at an event, for the Vendors section of its
    /// page. A vendor can't apply again once approved, so each appears once.
    /// </summary>
    public Task<List<EventVendorViewModel>> GetEventVendorsAsync(int eventId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorProfiles
            .AsNoTracking()
            .Where(profile => profile.Applications.Any(application =>
                application.EventId == eventId &&
                application.Kind == VendorApplicationKind.Event &&
                application.Status == ApplicationStatus.Approved))
            .OrderBy(profile => profile.BusinessName)
            .Select(profile => new EventVendorViewModel
            {
                VendorId = profile.Id,
                BusinessName = profile.BusinessName,
                ProductsOffered = profile.ProductsOffered,
                HasStorefront = profile.Applications.Any(application =>
                    application.Kind == VendorApplicationKind.OnlineSales &&
                    application.Status == ApplicationStatus.Approved)
            })
            .ToListAsync(cancellationToken);

    private Task<bool> IsApprovedForOnlineSalesAsync(int vendorProfileId, CancellationToken cancellationToken) =>
        _dbContext.VendorApplications.AnyAsync(application =>
            application.VendorProfileId == vendorProfileId &&
            application.Kind == VendorApplicationKind.OnlineSales &&
            application.Status == ApplicationStatus.Approved,
            cancellationToken);

    private static IQueryable<ProductCardViewModel> ToCards(IQueryable<VendorProduct> products) =>
        products.Select(product => new ProductCardViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            QuantityInStock = product.QuantityInStock,
            ImageId = product.ImageId,
            VendorId = product.VendorProfileId,
            VendorName = product.VendorProfile.BusinessName
        });

    private static VendorProductResult Fail(string message, int? productId = null, string? field = null) =>
        new(false, message, productId, field);
}
