using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// Records whether a student was present for a given week's session.
/// </summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    public int CourseEnrolmentId { get; set; }
    [ValidateNever]
    public CourseEnrolment CourseEnrolment { get; set; } = null!;

    [Range(1, 52)]
    [Display(Name = "Week Number")]
    public int WeekNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Session Date")]
    public DateTime SessionDate { get; set; }

    public bool Present { get; set; }
}
