using Microsoft.AspNetCore.Authentication.Cookies; // 🚀 ADDED THIS
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Services;


var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // FIX 1: Unique name prevents collision with HRMS
        options.Cookie.Name = "RadarOne_SSO_Auth";

        // FIX 2: Ensure the cookie applies to the whole application
        options.Cookie.Path = "/";

        options.LoginPath = "/Users/Login";
        options.AccessDeniedPath = "/Users/Login";
    });

// Add this line where your other services are registered (like builder.Services.AddControllersWithViews();)
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

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
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
    pattern: "{controller=Users}/{action=Login}/{id?}") // 🚀 CHANGED THIS: Defaults to Login page
    .WithStaticAssets();

app.Run();
