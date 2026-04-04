using Microsoft.AspNetCore.Identity;

namespace VgcCollege.Web.Models;

/// <summary>
/// Extends the default Identity user with a human-readable display name.
/// Navigation properties back to profiles are intentionally omitted here;
/// profiles are looked up via IdentityUserId FK instead.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Friendly name shown in the navbar (set during seeding / registration).</summary>
    public string DisplayName { get; set; } = string.Empty;
}
