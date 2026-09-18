using Microsoft.AspNetCore.Authentication.Cookies; // 🚀 ADDED THIS
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Services;


var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

// 🚀 ADDED THIS: Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Users/Login"; // Redirects here if not logged in
        options.ExpireTimeSpan = TimeSpan.FromDays(1); // Keeps user logged in for 1 day
    });

// Add this line where your other services are registered (like builder.Services.AddControllersWithViews();)
builder.Services.AddScoped<IHierarchyService, HierarchyService>();

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