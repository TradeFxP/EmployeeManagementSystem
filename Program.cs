using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using UserRoles.Data;
using UserRoles.Helpers;
using UserRoles.Models;
using UserRoles.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using EmployeeManagementSystem.Middleware;


var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;





// ================= MVC =================
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ================= DATABASE =================
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

// ================= IDENTITY =================
builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ================= EMAIL =================
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddHttpClient<IEmailService, EmailService>();
builder.Services.AddScoped<ITaskHistoryService, TaskHistoryService>();
builder.Services.AddScoped<ITaskPermissionService, TaskPermissionService>();
builder.Services.AddScoped<IUserHierarchyService, UserHierarchyService>();

// ================= TASK & IMAGE SERVICES =================
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

// ================= FACEBOOK LEADS =================
builder.Services.AddHttpClient<IFacebookLeadsService, FacebookLeadsService>();
builder.Services.AddHostedService<FacebookLeadIngestionService>();

// ================= PERFORMANCE =================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.AddMemoryCache();

// ================= RATE LIMITING =================
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ================= REQUEST SIZE LIMITS =================
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});

// ================= COOKIE =================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});





// ================= APP =================
var app = builder.Build();

// ================= SEED =================
await SeedService.SeedDatabase(app.Services);

// ================= PIPELINE =================
app.UseGlobalExceptionHandler();
app.UseSecurityHeaders();
app.UseMiddleware<EmployeeManagementSystem.Middleware.AjaxRedirectMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseResponseCompression();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ================= ROUTING =================
app.MapHub<UserRoles.Hubs.TaskHub>("/taskHub");

// 🔴 IMPORTANT: DEFAULT ROUTE → RedirectByRole so authenticated Admin/Manager go to OrgChart
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=RedirectByRole}/{id?}");


//app.Urls.Add("http://+:8080");


app.Run();
