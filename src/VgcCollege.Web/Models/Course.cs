using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

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

    [Display(Name = "Branch")]
    public int BranchId { get; set; }

    // ValidateNever prevents MVC from requiring the navigation object to be
    // posted back – only the FK int (BranchId) is needed from the form.
    [ValidateNever]
    public Branch Branch { get; set; } = null!;

    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; }

    [ValidateNever]
    public ICollection<CourseEnrolment> Enrolments { get; set; } = new List<CourseEnrolment>();
    [ValidateNever]
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    [ValidateNever]
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
    [ValidateNever]
    public ICollection<FacultyCourseAssignment> FacultyAssignments { get; set; } = new List<FacultyCourseAssignment>();
}
