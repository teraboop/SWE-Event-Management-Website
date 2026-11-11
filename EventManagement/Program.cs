using System.Globalization;
using EventManagement.Data;
using EventManagement.Services;
using EventManagement.Services.Ai;
using EventManagement.Services.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Prices, dates and number inputs use US formatting everywhere. Without
// this, a Linux host with no locale set (like the Render container) would
// show prices as "¤10.00".
var appCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = appCulture;
CultureInfo.DefaultThreadCurrentUICulture = appCulture;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render tell the app which port to listen on through PORT.
// Locally it isn't set, so launchSettings.json applies as before.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<EventDiscoveryService>();
builder.Services.AddScoped<TicketPurchaseService>();
builder.Services.AddScoped<VendorApplicationService>();
builder.Services.AddScoped<PanelApplicationService>();
builder.Services.AddScoped<EventImageService>();
builder.Services.AddScoped<EventApplicationService>();
builder.Services.AddScoped<EventReviewService>();
builder.Services.AddScoped<VendorProductService>();
builder.Services.AddScoped<EventAnnouncementService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<RewardService>();

// Email: services queue messages, and EmailDispatcher sends them in the
// background through Brevo (or writes them to the log without a Brevo key).
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailDispatcher>();
builder.Services.AddHttpClient<BrevoEmailDelivery>(client => client.BaseAddress = new Uri("https://api.brevo.com/"));
builder.Services.AddTransient<LogEmailDelivery>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, QueuedEmailSender>();
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddHttpContextAccessor();

// AI features (Gemini): the chat assistant and recommendations. Without a
// Gemini:ApiKey the chat is hidden and recommendations use simple rules.
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<GeminiClient>(client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
builder.Services.AddSingleton<AiUsageLimiter>();
builder.Services.AddScoped<AssistantService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.TryAddSingleton(TimeProvider.System);

// Store the sign-in encryption keys in the database (see ApplicationDbContext.DataProtectionKeys).
builder.Services.AddDataProtection()
    .SetApplicationName("EventManagement")
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();