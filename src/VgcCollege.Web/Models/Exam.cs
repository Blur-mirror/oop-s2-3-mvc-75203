using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// An exam belonging to a course.
/// ResultsReleased controls whether students can see their ExamResult rows.
/// </summary>
public class Exam
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    [ValidateNever]
    public Course Course { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Range(1, 10000)]
    [Display(Name = "Max Score")]
    public decimal MaxScore { get; set; }

    [Display(Name = "Results Released")]
    public bool ResultsReleased { get; set; } = false;

    [ValidateNever]
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
