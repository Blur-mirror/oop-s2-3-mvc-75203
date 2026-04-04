using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// Extended profile for a student user.
/// Linked 1-to-1 with an ASP.NET Identity ApplicationUser.
/// </summary>
public class StudentProfile
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

    [MaxLength(300)]
    public string? Address { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime DateOfBirth { get; set; }

    [Required, MaxLength(20)]
    [Display(Name = "Student Number")]
    public string StudentNumber { get; set; } = string.Empty;

    // ── Navigation ──────────────────────────────────────────────────────────
    public ICollection<CourseEnrolment> Enrolments { get; set; } = new List<CourseEnrolment>();
    public ICollection<AssignmentResult> AssignmentResults { get; set; } = new List<AssignmentResult>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
}
