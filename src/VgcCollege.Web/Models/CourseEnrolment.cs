using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// Records a student's enrolment in a specific course.
/// Cascade deletes attendance records when an enrolment is removed.
/// </summary>
public class CourseEnrolment
{
    public int Id { get; set; }

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [DataType(DataType.Date)]
    [Display(Name = "Enrol Date")]
    public DateTime EnrolDate { get; set; } = DateTime.Today;

    /// <summary>Active | Withdrawn | Completed</summary>
    [Required, MaxLength(30)]
    public string Status { get; set; } = "Active";

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>Weekly attendance records for this enrolment.</summary>
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
