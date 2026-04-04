using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// The score and optional feedback a student received for an assignment.
/// Always visible (only exam results have a release gate).
/// </summary>
public class AssignmentResult
{
    public int Id { get; set; }

    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;

    [Range(0, 10000)]
    public decimal Score { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }

    /// <summary>Computed percentage – used for display and grade calculations.</summary>
    public decimal Percentage => Assignment?.MaxScore > 0
        ? Math.Round(Score / Assignment.MaxScore * 100, 1)
        : 0;
}
