using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// A course offered at a branch over a defined period.
/// Faculty are assigned to courses; students enrol in them.
/// </summary>
public class Course
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    // ── Branch FK ───────────────────────────────────────────────────────────
    [Display(Name = "Branch")]
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────────
    public ICollection<CourseEnrolment> Enrolments { get; set; } = new List<CourseEnrolment>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();

    /// <summary>Faculty assigned to teach this course.</summary>
    public ICollection<FacultyCourseAssignment> FacultyAssignments { get; set; } = new List<FacultyCourseAssignment>();
}
