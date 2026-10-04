using EventManagement.Models.Events;
using EventManagement.Models.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        string[] roles =
        [
            "Admin",
            "Organizer",
            "Attendee"
        ];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        string[] interestCategoryNames =
        [
            "Arts & Culture",
            "Business",
            "Food & Drink",
            "Gaming",
            "Health & Fitness",
            "Music",
            "Sports",
            "Technology"
        ];

        foreach (var categoryName in interestCategoryNames)
        {
            var exists = await context.InterestCategories
                .AnyAsync(category => category.Name == categoryName);

            if (!exists)
            {
                context.InterestCategories.Add(new InterestCategory
                {
                    Name = categoryName
                });
            }
        }

        string[] eventCategoryNames =
        [
            "Conference",
            "Concert",
            "Festival",
            "Gaming",
            "Networking",
            "Sports",
            "Workshop"
        ];

        foreach (var categoryName in eventCategoryNames)
        {
            var exists = await context.EventCategories
                .AnyAsync(category => category.Name == categoryName);

            if (!exists)
            {
                context.EventCategories.Add(new EventCategory
                {
                    Name = categoryName
                });
            }
        }

        await context.SaveChangesAsync();
    }
}