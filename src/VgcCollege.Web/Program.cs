using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=vgccollege.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── ASP.NET Identity ──────────────────────────────────────────────────────────
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount  = false;
    options.Password.RequiredLength        = 8;
    options.Password.RequireDigit          = true;
    options.Password.RequireUppercase      = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Authorization policies ────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",    p => p.RequireRole("Administrator"));
    options.AddPolicy("FacultyOnly",  p => p.RequireRole("Faculty"));
    options.AddPolicy("StudentOnly",  p => p.RequireRole("Student"));
    options.AddPolicy("StaffOrAdmin", p => p.RequireRole("Administrator", "Faculty"));
});

var app = builder.Build();

// ── Seed database on startup ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.EnsureSeeded(scope.ServiceProvider);
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

// Expose Program to the integration test project
public partial class Program { }
