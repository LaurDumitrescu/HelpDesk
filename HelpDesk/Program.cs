using HelpdeskApp.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext configuration
builder.Services.AddDbContext<HelpdeskContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add session services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(3); // Set session timeout to 3 hours
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Configure cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(3); // Match session timeout to 3 hours
        options.SlidingExpiration = true;
    });

// Configure logging
var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
if (!Directory.Exists(logsPath))
{
    Directory.CreateDirectory(logsPath);
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile(Path.Combine(logsPath, "log.txt"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Helpdesk/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}");
    endpoints.MapControllerRoute(
        name: "helpdesk",
        pattern: "{controller=Helpdesk}/{action=Index}/{id?}");
    endpoints.MapControllerRoute(
        name: "viewentries",
        pattern: "{controller=Helpdesk}/{action=ViewEntries}/{id?}");
    endpoints.MapControllerRoute(
        name: "getentrydetails",
        pattern: "Helpdesk/GetEntryDetails/{id}",
        defaults: new { controller = "Helpdesk", action = "GetEntryDetails" });
    endpoints.MapControllerRoute(
        name: "modifyentry",
        pattern: "Helpdesk/ModifyEntry/{id}",
        defaults: new { controller = "Helpdesk", action = "ModifyEntry" });
});

app.Run();
