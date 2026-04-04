using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// Extended profile for a faculty (staff) user.
/// Linked 1-to-1 with an ASP.NET Identity ApplicationUser.
/// </summary>
public class FacultyProfile
{
    public int Id { get; set; }

    /// <summary>FK to AspNetUsers.Id (string GUID).</summary>
    [Required]
    public string IdentityUserId { get; set; } = string.Empty;
    public ApplicationUser IdentityUser { get; set; } = null!;

    [Required, MaxLength(120)]
    [Display(Name = "Full Name")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, MaxLength(30)]
    public string? Phone { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Courses this faculty member is assigned to.</summary>
    public ICollection<FacultyCourseAssignment> CourseAssignments { get; set; } = new List<FacultyCourseAssignment>();
}
