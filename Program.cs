using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserRoles.Data;
using UserRoles.Models;
using UserRoles.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;



System.Net.ServicePointManager.SecurityProtocol =
    System.Net.SecurityProtocolType.Tls12;


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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

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
