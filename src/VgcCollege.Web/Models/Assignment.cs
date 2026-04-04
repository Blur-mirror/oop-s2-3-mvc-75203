using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// An assessed assignment belonging to a course.
/// </summary>
public class Assignment
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    [ValidateNever]
    public Course Course { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 10000)]
    [Display(Name = "Max Score")]
    public decimal MaxScore { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Due Date")]
    public DateTime DueDate { get; set; }

    [ValidateNever]
    public ICollection<AssignmentResult> Results { get; set; } = new List<AssignmentResult>();
}
