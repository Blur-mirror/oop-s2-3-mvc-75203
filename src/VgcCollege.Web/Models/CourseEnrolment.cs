using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// Records a student's enrolment in a specific course.
/// </summary>
public class CourseEnrolment
{
    public int Id { get; set; }

    public int StudentProfileId { get; set; }
    [ValidateNever]
    public StudentProfile StudentProfile { get; set; } = null!;

    public int CourseId { get; set; }
    [ValidateNever]
    public Course Course { get; set; } = null!;

    [DataType(DataType.Date)]
    [Display(Name = "Enrol Date")]
    public DateTime EnrolDate { get; set; } = DateTime.Today;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    [ValidateNever]
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
