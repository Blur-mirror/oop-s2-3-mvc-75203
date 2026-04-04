using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// An exam belonging to a course.
/// ResultsReleased controls whether students can see their ExamResult rows.
/// </summary>
public class Exam
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Range(1, 10000)]
    [Display(Name = "Max Score")]
    public decimal MaxScore { get; set; }

    /// <summary>
    /// When false, students see "Provisional – not yet released" instead of their score.
    /// Admin can flip this flag; the check is enforced server-side in the controller.
    /// </summary>
    [Display(Name = "Results Released")]
    public bool ResultsReleased { get; set; } = false;

    // ── Navigation ──────────────────────────────────────────────────────────
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
