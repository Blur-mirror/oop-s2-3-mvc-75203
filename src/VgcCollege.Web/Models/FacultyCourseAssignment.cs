using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// Join table: assigns a faculty member to a course.
/// Role "Tutor" grants access to student contact details.
/// </summary>
public class FacultyCourseAssignment
{
    public int Id { get; set; }

    public int FacultyProfileId { get; set; }
    [ValidateNever]
    public FacultyProfile FacultyProfile { get; set; } = null!;

    public int CourseId { get; set; }
    [ValidateNever]
    public Course Course { get; set; } = null!;

    [Required, MaxLength(60)]
    public string Role { get; set; } = "Lecturer";
}
