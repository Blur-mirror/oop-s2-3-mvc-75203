using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// Extended profile for a student user.
/// </summary>
public class StudentProfile
{
    public int Id { get; set; }

    [Required]
    public string IdentityUserId { get; set; } = string.Empty;
    [ValidateNever]
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

    [ValidateNever]
    public ICollection<CourseEnrolment> Enrolments { get; set; } = new List<CourseEnrolment>();
    [ValidateNever]
    public ICollection<AssignmentResult> AssignmentResults { get; set; } = new List<AssignmentResult>();
    [ValidateNever]
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
}
