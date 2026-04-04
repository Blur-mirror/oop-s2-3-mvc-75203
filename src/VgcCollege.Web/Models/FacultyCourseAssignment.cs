using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// Join table: assigns a faculty member to a course.
/// A single course can have multiple faculty; a faculty member can teach multiple courses.
/// </summary>
public class FacultyCourseAssignment
{
    public int Id { get; set; }

    public int FacultyProfileId { get; set; }
    public FacultyProfile FacultyProfile { get; set; } = null!;

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    /// <summary>
    /// Role within the course, e.g. "Tutor", "Lecturer", "Lab Assistant".
    /// Faculty with the Tutor role can view student contact details.
    /// </summary>
    [Required, MaxLength(60)]
    public string Role { get; set; } = "Lecturer";
}
