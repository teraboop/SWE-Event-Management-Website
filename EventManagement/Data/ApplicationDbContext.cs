using EventManagement.Models.Commerce;
using EventManagement.Models.Events;
using EventManagement.Models.Panels;
using EventManagement.Models.Profile;
using EventManagement.Models.Reviews;
using EventManagement.Models.Vendors;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<InterestCategory> InterestCategories => Set<InterestCategory>();

    public DbSet<UserInterest> UserInterests => Set<UserInterest>();

    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<SavedEvent> SavedEvents => Set<SavedEvent>();

    public DbSet<EventInterest> EventInterests => Set<EventInterest>();

    public DbSet<EventTicketType> EventTicketTypes => Set<EventTicketType>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<VendorProfile> VendorProfiles => Set<VendorProfile>();

    public DbSet<VendorApplication> VendorApplications => Set<VendorApplication>();

    public DbSet<PanelApplication> PanelApplications => Set<PanelApplication>();

    public DbSet<EventImage> EventImages => Set<EventImage>();

    public DbSet<EventApplication> EventApplications => Set<EventApplication>();

    public DbSet<EventApplicationInterest> EventApplicationInterests => Set<EventApplicationInterest>();

    public DbSet<EventReview> EventReviews => Set<EventReview>();

    public DbSet<VendorProduct> VendorProducts => Set<VendorProduct>();

    public DbSet<EventAnnouncement> EventAnnouncements => Set<EventAnnouncement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().Property(user => user.TimeZoneId).HasMaxLength(100);

        builder.Entity<InterestCategory>(entity =>
        {
            entity.ToTable("InterestCategories");

            entity.Property(category => category.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(category => category.Name)
                .IsUnique();
        });

        builder.Entity<UserInterest>(entity =>
        {
            entity.ToTable("UserInterests");

            entity.HasKey(userInterest => new
            {
                userInterest.UserId,
                userInterest.InterestCategoryId
            });

            entity.HasOne(userInterest => userInterest.User)
                .WithMany(user => user.Interests)
                .HasForeignKey(userInterest => userInterest.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(userInterest => userInterest.InterestCategory)
                .WithMany(category => category.UserInterests)
                .HasForeignKey(userInterest => userInterest.InterestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventCategory>(entity =>
        {
            entity.ToTable("EventCategories");

            entity.Property(category => category.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(category => category.Name)
                .IsUnique();
        });

        builder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");

            entity.Property(@event => @event.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(@event => @event.VenueName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(@event => @event.City)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(@event => @event.PostalCode)
                .HasMaxLength(20);

            entity.Property(@event => @event.MinimumPrice)
                .HasPrecision(10, 2);

            entity.Property(@event => @event.ExternalWebsiteUrl)
                .HasMaxLength(2048);

            entity.HasIndex(@event => new
            {
                @event.Status,
                @event.StartsAt
            });

            entity.HasIndex(@event => @event.City);

            entity.HasOne(@event => @event.Organizer)
                .WithMany()
                .HasForeignKey(@event => @event.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(@event => @event.EventCategory)
                .WithMany(category => category.Events)
                .HasForeignKey(@event => @event.EventCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(@event => @event.TicketSalesMode)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(@event => @event.ExternalTicketingUrl)
                .HasMaxLength(2048);
        });

        builder.Entity<EventInterest>(entity =>
        {
            entity.ToTable("EventInterests");

            entity.HasKey(eventInterest => new
            {
                eventInterest.EventId,
                eventInterest.InterestCategoryId
            });

            entity.HasOne(eventInterest => eventInterest.Event)
                .WithMany(@event => @event.Interests)
                .HasForeignKey(eventInterest => eventInterest.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(eventInterest => eventInterest.InterestCategory)
                .WithMany(category => category.EventInterests)
                .HasForeignKey(eventInterest => eventInterest.InterestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(eventInterest => eventInterest.InterestCategoryId);
        });

        builder.Entity<SavedEvent>(entity =>
        {
            entity.ToTable("SavedEvents");

            entity.HasKey(savedEvent => new
            {
                savedEvent.UserId,
                savedEvent.EventId
            });

            entity.HasOne(savedEvent => savedEvent.User)
                .WithMany(user => user.SavedEvents)
                .HasForeignKey(savedEvent => savedEvent.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(savedEvent => savedEvent.Event)
                .WithMany(@event => @event.SavedByUsers)
                .HasForeignKey(savedEvent => savedEvent.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventTicketType>(entity =>
        {
            entity.ToTable("EventTicketTypes", table =>
            {
                table.HasCheckConstraint(
                    "CK_EventTicketTypes_Price_NonNegative",
                    "[Price] >= 0");

                table.HasCheckConstraint(
                    "CK_EventTicketTypes_QuantityAvailable_NonNegative",
                    "[QuantityAvailable] >= 0");

                table.HasCheckConstraint(
                    "CK_EventTicketTypes_SalesWindow",
                    "[SalesEndAt] > [SalesStartAt]");

                // The database's own guarantee against overselling: no write,
                // from a purchase or an organizer lowering capacity, can leave
                // more tickets sold than exist.
                table.HasCheckConstraint(
                    "CK_EventTicketTypes_QuantitySold_WithinCapacity",
                    "[QuantitySold] >= 0 AND [QuantitySold] <= [QuantityAvailable]");
            });

            entity.Property(ticketType => ticketType.QuantitySold)
                .HasDefaultValue(0);

            entity.Property(ticketType => ticketType.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(ticketType => ticketType.Price)
                .HasPrecision(10, 2)
                .IsRequired();

            entity.Property(ticketType => ticketType.QuantityAvailable)
                .IsRequired();

            entity.Property(ticketType => ticketType.SalesStartAt)
                .IsRequired();

            entity.Property(ticketType => ticketType.SalesEndAt)
                .IsRequired();

            entity.Property(ticketType => ticketType.IsEnabled)
                .HasDefaultValue(true);

            entity.HasIndex(ticketType => new
            {
                ticketType.EventId,
                ticketType.Name
            })
            .IsUnique();

            entity.HasIndex(ticketType => new
            {
                ticketType.EventId,
                ticketType.IsEnabled
            });

            entity.HasOne(ticketType => ticketType.Event)
                .WithMany(@event => @event.TicketTypes)
                .HasForeignKey(ticketType => ticketType.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders", table =>
            {
                table.HasCheckConstraint(
                    "CK_Orders_TotalAmount_NonNegative",
                    "[TotalAmount] >= 0");
            });

            entity.Property(order => order.UserId)
                .IsRequired();

            entity.Property(order => order.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(order => order.TotalAmount)
                .HasPrecision(10, 2);

            entity.HasIndex(order => new
            {
                order.UserId,
                order.Status
            });

            // Deleting an account deletes its orders, matching SavedEvents, so
            // the Identity "delete my data" page keeps working.
            entity.HasOne(order => order.User)
                .WithMany()
                .HasForeignKey(order => order.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets", table =>
            {
                table.HasCheckConstraint(
                    "CK_Tickets_UnitPrice_NonNegative",
                    "[UnitPrice] >= 0");
            });

            entity.Property(ticket => ticket.UnitPrice)
                .HasPrecision(10, 2);

            entity.HasIndex(ticket => ticket.Code)
                .IsUnique();

            entity.HasOne(ticket => ticket.Order)
                .WithMany(order => order.Tickets)
                .HasForeignKey(ticket => ticket.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // A ticket type that has sold tickets cannot be deleted out from
            // under its buyers; organizers disable it instead.
            entity.HasOne(ticket => ticket.EventTicketType)
                .WithMany(ticketType => ticketType.Tickets)
                .HasForeignKey(ticket => ticket.EventTicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VendorProfile>(entity =>
        {
            entity.ToTable("VendorProfiles");

            entity.Property(profile => profile.BusinessName).HasMaxLength(150).IsRequired();
            entity.Property(profile => profile.ProductsOffered).HasMaxLength(500).IsRequired();
            entity.Property(profile => profile.Description).HasMaxLength(2000).IsRequired();
            entity.Property(profile => profile.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(profile => profile.WebsiteUrl).HasMaxLength(2048);

            // One vendor identity per account.
            entity.HasIndex(profile => profile.OwnerUserId)
                .IsUnique();

            entity.HasOne(profile => profile.Owner)
                .WithMany()
                .HasForeignKey(profile => profile.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VendorApplication>(entity =>
        {
            entity.ToTable("VendorApplications", table =>
            {
                // Event applications must name an event; online ones must not.
                table.HasCheckConstraint(
                    "CK_VendorApplications_EventMatchesKind",
                    "([Kind] = 1 AND [EventId] IS NOT NULL) OR ([Kind] = 0 AND [EventId] IS NULL)");
            });

            entity.Property(application => application.Kind).HasConversion<int>();
            entity.Property(application => application.Status).HasConversion<int>();

            entity.Property(application => application.BusinessName).HasMaxLength(150).IsRequired();
            entity.Property(application => application.ProductsOffered).HasMaxLength(500).IsRequired();
            entity.Property(application => application.Description).HasMaxLength(2000).IsRequired();
            entity.Property(application => application.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(application => application.WebsiteUrl).HasMaxLength(2048);
            entity.Property(application => application.Message).HasMaxLength(1000);
            entity.Property(application => application.ReviewedByUserId).HasMaxLength(450);
            entity.Property(application => application.ReviewedByName).HasMaxLength(256);
            entity.Property(application => application.ReviewNote).HasMaxLength(1000);

            // At most one pending application per vendor for the same thing
            // (online sales, or one particular event). SQL Server treats NULL
            // EventIds as equal in a unique index, so this also covers online
            // applications. Decided applications are outside the filter, so
            // vendors can reapply after a rejection.
            entity.HasIndex(application => new
            {
                application.VendorProfileId,
                application.Kind,
                application.EventId
            })
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("IX_VendorApplications_OnePending");

            // Review queues: pending applications by kind or by event.
            entity.HasIndex(application => new { application.Kind, application.Status });
            entity.HasIndex(application => new { application.EventId, application.Status });

            entity.HasOne(application => application.VendorProfile)
                .WithMany(profile => profile.Applications)
                .HasForeignKey(application => application.VendorProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(application => application.Event)
                .WithMany(@event => @event.VendorApplications)
                .HasForeignKey(application => application.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PanelApplication>(entity =>
        {
            entity.ToTable("PanelApplications", table =>
            {
                table.HasCheckConstraint(
                    "CK_PanelApplications_DurationMinutes",
                    "[DurationMinutes] BETWEEN 15 AND 240");
            });

            entity.Property(application => application.Status).HasConversion<int>();

            entity.Property(application => application.Title).HasMaxLength(150).IsRequired();
            entity.Property(application => application.Description).HasMaxLength(2000).IsRequired();
            entity.Property(application => application.Panelists).HasMaxLength(1000).IsRequired();
            entity.Property(application => application.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(application => application.Message).HasMaxLength(1000);
            entity.Property(application => application.ReviewedByUserId).HasMaxLength(450);
            entity.Property(application => application.ReviewedByName).HasMaxLength(256);
            entity.Property(application => application.ReviewNote).HasMaxLength(1000);

            // No two pending proposals with the same title from one user for
            // one event, which also catches a form submitted twice. Different
            // panels from the same user are fine.
            entity.HasIndex(application => new
            {
                application.EventId,
                application.ApplicantUserId,
                application.Title
            })
            .IsUnique()
            .HasFilter("[Status] = 0")
            .HasDatabaseName("IX_PanelApplications_OnePendingPerTitle");

            // Review queue and the public list of approved panels.
            entity.HasIndex(application => new { application.EventId, application.Status });

            entity.HasOne(application => application.Applicant)
                .WithMany()
                .HasForeignKey(application => application.ApplicantUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(application => application.Event)
                .WithMany(@event => @event.PanelApplications)
                .HasForeignKey(application => application.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventImage>(entity =>
        {
            entity.ToTable("EventImages");

            entity.Property(image => image.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(image => image.Data).IsRequired();
            entity.Property(image => image.UploadedByUserId).HasMaxLength(450);
        });

        // Images are shared by an application and the event made from it, so
        // neither side may delete them out from under the other.
        builder.Entity<Event>()
            .HasOne(@event => @event.BackgroundImage)
            .WithMany()
            .HasForeignKey(@event => @event.BackgroundImageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EventApplication>(entity =>
        {
            entity.ToTable("EventApplications", table =>
            {
                table.HasCheckConstraint(
                    "CK_EventApplications_EndsAfterStart",
                    "[EndsAt] > [StartsAt]");

                // Approved applications point at the event they created; no others do.
                table.HasCheckConstraint(
                    "CK_EventApplications_CreatedEventMatchesStatus",
                    "([Status] = 1 AND [CreatedEventId] IS NOT NULL) OR ([Status] <> 1 AND [CreatedEventId] IS NULL)");
            });

            entity.Property(application => application.Status).HasConversion<int>();

            // Listing fields use the same limits as Events.
            entity.Property(application => application.Title).HasMaxLength(200).IsRequired();
            entity.Property(application => application.Description).HasMaxLength(4000).IsRequired();
            entity.Property(application => application.VenueName).HasMaxLength(200).IsRequired();
            entity.Property(application => application.City).HasMaxLength(100).IsRequired();
            entity.Property(application => application.PostalCode).HasMaxLength(20);
            entity.Property(application => application.ExternalWebsiteUrl).HasMaxLength(2048);

            entity.Property(application => application.ContactName).HasMaxLength(150).IsRequired();
            entity.Property(application => application.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(application => application.ContactPhone).HasMaxLength(30);
            entity.Property(application => application.OrganizationName).HasMaxLength(150);
            entity.Property(application => application.Message).HasMaxLength(1000);
            entity.Property(application => application.ReviewedByUserId).HasMaxLength(450);
            entity.Property(application => application.ReviewedByName).HasMaxLength(256);
            entity.Property(application => application.ReviewNote).HasMaxLength(1000);

            // No two pending applications with the same title from one user,
            // which also catches a form submitted twice.
            entity.HasIndex(application => new { application.ApplicantUserId, application.Title })
                .IsUnique()
                .HasFilter("[Status] = 0")
                .HasDatabaseName("IX_EventApplications_OnePendingPerTitle");

            entity.HasIndex(application => new { application.Status, application.SubmittedAt });

            entity.HasOne(application => application.Applicant)
                .WithMany()
                .HasForeignKey(application => application.ApplicantUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(application => application.EventCategory)
                .WithMany()
                .HasForeignKey(application => application.EventCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(application => application.BackgroundImage)
                .WithMany()
                .HasForeignKey(application => application.BackgroundImageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(application => application.CreatedEvent)
                .WithMany()
                .HasForeignKey(application => application.CreatedEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventApplicationInterest>(entity =>
        {
            entity.ToTable("EventApplicationInterests");

            entity.HasKey(interest => new { interest.EventApplicationId, interest.InterestCategoryId });

            entity.HasOne(interest => interest.EventApplication)
                .WithMany(application => application.Interests)
                .HasForeignKey(interest => interest.EventApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(interest => interest.InterestCategory)
                .WithMany()
                .HasForeignKey(interest => interest.InterestCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EventReview>(entity =>
        {
            entity.ToTable("EventReviews", table =>
            {
                table.HasCheckConstraint(
                    "CK_EventReviews_Rating",
                    "[Rating] BETWEEN 1 AND 5");
            });

            entity.Property(review => review.Comment).HasMaxLength(2000);

            // One review per person per event; editing replaces it.
            entity.HasIndex(review => new { review.EventId, review.AuthorUserId })
                .IsUnique()
                .HasDatabaseName("IX_EventReviews_OnePerAuthor");

            // The event page lists the newest reviews first.
            entity.HasIndex(review => new { review.EventId, review.CreatedAt });

            entity.HasOne(review => review.Event)
                .WithMany(@event => @event.Reviews)
                .HasForeignKey(review => review.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting an account deletes its reviews, like its orders and saved events.
            entity.HasOne(review => review.Author)
                .WithMany()
                .HasForeignKey(review => review.AuthorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VendorProduct>(entity =>
        {
            entity.ToTable("VendorProducts", table =>
            {
                table.HasCheckConstraint(
                    "CK_VendorProducts_Price_Positive",
                    "[Price] > 0");

                // The future checkout takes stock with a conditional UPDATE;
                // this keeps any write from overselling.
                table.HasCheckConstraint(
                    "CK_VendorProducts_QuantityInStock_NonNegative",
                    "[QuantityInStock] >= 0");
            });

            entity.Property(product => product.Name).HasMaxLength(150).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(2000).IsRequired();
            entity.Property(product => product.Price).HasPrecision(10, 2);
            entity.Property(product => product.IsListed).HasDefaultValue(true);

            // The vendor's list, and the shop's listed, unarchived products newest first.
            entity.HasIndex(product => new { product.VendorProfileId, product.ArchivedAt });
            entity.HasIndex(product => new { product.IsListed, product.ArchivedAt, product.CreatedAt });

            entity.HasOne(product => product.VendorProfile)
                .WithMany(profile => profile.Products)
                .HasForeignKey(product => product.VendorProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(product => product.Image)
                .WithMany()
                .HasForeignKey(product => product.ImageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventAnnouncement>(entity =>
        {
            entity.ToTable("EventAnnouncements");

            entity.Property(announcement => announcement.Title).HasMaxLength(150).IsRequired();
            entity.Property(announcement => announcement.Body).HasMaxLength(2000).IsRequired();
            entity.Property(announcement => announcement.PostedByUserId).HasMaxLength(450);
            entity.Property(announcement => announcement.PostedByName).HasMaxLength(256);

            // The event page lists pinned announcements first, then newest.
            entity.HasIndex(announcement => new { announcement.EventId, announcement.IsPinned, announcement.CreatedAt });

            entity.HasOne(announcement => announcement.Event)
                .WithMany(@event => @event.Announcements)
                .HasForeignKey(announcement => announcement.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}