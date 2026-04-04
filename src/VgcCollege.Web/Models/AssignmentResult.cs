using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// The score and optional feedback a student received for an assignment.
/// Always visible (only exam results have a release gate).
/// </summary>
public class AssignmentResult
{
    public int Id { get; set; }

    public int AssignmentId { get; set; }
    [ValidateNever]
    public Assignment Assignment { get; set; } = null!;

    public int StudentProfileId { get; set; }
    [ValidateNever]
    public StudentProfile StudentProfile { get; set; } = null!;

    // No step restriction – any decimal allowed; we round to 2dp on save
    [Range(0, 10000)]
    public decimal Score { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }

    public decimal Percentage => Assignment?.MaxScore > 0
        ? Math.Round(Score / Assignment.MaxScore * 100, 1)
        : 0;
}
