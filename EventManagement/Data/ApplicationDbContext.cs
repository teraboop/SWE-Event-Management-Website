using EventManagement.Models.Events;
using EventManagement.Models.Profile;
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
            });

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
    }
}