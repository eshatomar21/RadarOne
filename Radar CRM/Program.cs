using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Services;
using Microsoft.AspNetCore.DataProtection; // 🚀 ADDED THIS
using System.IO; // 🚀 ADDED THIS

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

// 🚀 FIX 1: Save encryption keys to a physical folder so IIS recycles don't destroy them
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"C:\CRM_Files\DataProtectionKeys"))
    .SetApplicationName("RadarONE_CRM");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Unique name prevents collision with HRMS
        options.Cookie.Name = "RadarOne_SSO_Auth";

        // Ensure the cookie applies to the whole application
        options.Cookie.Path = "/";

        options.LoginPath = "/Users/Login";
        options.AccessDeniedPath = "/Users/Login";

        // 🚀 FIX 2: Set cookie to survive for 30 days and renew automatically
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Add this line where your other services are registered
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

builder.Services.AddScoped<AiCallService>();


builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 104857600; // 100 MB limit
    options.MemoryBufferThreshold = int.MaxValue;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// 🚀 ADDED THIS: Must be exactly here, between UseRouting and UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Users}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
