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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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

            entity.HasOne(@event => @event.Organizer)
                .WithMany()
                .HasForeignKey(@event => @event.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(@event => @event.EventCategory)
                .WithMany(category => category.Events)
                .HasForeignKey(@event => @event.EventCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
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
    }
}