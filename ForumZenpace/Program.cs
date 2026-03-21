using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using ForumZenpace.Hubs;
using ForumZenpace.Models;
using ForumZenpace.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.Configure<EmailJsSettings>(builder.Configuration.GetSection("EmailJsSettings"));
builder.Services.AddHttpClient<IEmailSender, EmailJsEmailSender>();
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddScoped<PasswordSecurityService>();
builder.Services.AddScoped<AuthFlowTokenService>();
builder.Services.AddScoped<DirectMessageService>();
builder.Services.AddScoped<SocialService>();
builder.Services.AddScoped<StoryMusicLibraryService>();
builder.Services.AddScoped<StoryService>();

// Setup DbContext
builder.Services.AddDbContext<ForumDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Setup Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

var app = builder.Build();
var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var uploadsRootPath = Path.Combine(webRootPath, "uploads");

Directory.CreateDirectory(Path.Combine(uploadsRootPath, "avatars"));
Directory.CreateDirectory(Path.Combine(uploadsRootPath, "posts"));
Directory.CreateDirectory(Path.Combine(uploadsRootPath, "stories"));
Directory.CreateDirectory(Path.Combine(uploadsRootPath, "story-music"));
Directory.CreateDirectory(Path.Combine(webRootPath, "library", "story-music"));
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "StoryMusic"));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRootPath),
    RequestPath = "/uploads"
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<DirectMessageHub>("/hubs/direct-messages");
app.MapHub<SocialHub>("/hubs/social");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Seed Database
using (var scope = app.Services.CreateScope())
{
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ForumDbContext>();
            var passwordSecurityService = services.GetRequiredService<PasswordSecurityService>();
            await DbInitializer.Initialize(context, passwordSecurityService);
        }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw;
    }
}

app.Run();
