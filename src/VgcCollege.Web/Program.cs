using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

var builder = WebApplication.CreateBuilder(args);

//Services Configuration

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=vgccollege.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ASP.NET Identity
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// MVC
builder.Services.AddControllersWithViews();

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Administrator"));
    options.AddPolicy("FacultyOnly", p => p.RequireRole("Faculty"));
    options.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
    options.AddPolicy("StaffOrAdmin", p => p.RequireRole("Administrator", "Faculty"));
});

var app = builder.Build();

// Seed database on startup
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.EnsureSeeded(scope.ServiceProvider);
}

//Middleware Pipeline
// Order is critical first registered = outermost = runs first on request,
// last on response.

// 1. Exception handler, outermost so it catches errors from everything below
app.UseMiddleware<VgcCollege.Web.Middleware.GlobalExceptionMiddleware>();

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

// 2. Timing, wraps auth + controller execution
// Placed here to include the latency of the authentication/authorization process
app.UseMiddleware<VgcCollege.Web.Middleware.RequestTimingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// 3. User enrichment, after auth so User.Identity is populated
// If placed before Authentication, User.Identity.IsAuthenticated would always be false.
app.UseMiddleware<VgcCollege.Web.Middleware.UserEnrichmentMiddleware>();

//Endpoints

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

// Expose Program to the integration test project
public partial class Program { }
