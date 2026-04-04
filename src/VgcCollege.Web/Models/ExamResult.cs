using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// The score and grade a student received in an exam.
/// Visibility is gated by Exam.ResultsReleased – enforced server-side.
/// </summary>
public class ExamResult
{
    public int Id { get; set; }

    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;

    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;

    [Range(0, 10000)]
    public decimal Score { get; set; }

    /// <summary>Letter grade: A, B, C, D, F – calculated on save.</summary>
    [MaxLength(2)]
    public string Grade { get; set; } = string.Empty;

    /// <summary>
    /// Derives a letter grade from the percentage score against MaxScore.
    /// Called in the service layer before persisting.
    /// </summary>
    public static string CalculateGrade(decimal score, decimal maxScore)
    {
        if (maxScore <= 0) return "F";
        var pct = score / maxScore * 100;
        return pct switch
        {
            >= 85 => "A",
            >= 70 => "B",
            >= 55 => "C",
            >= 40 => "D",
            _      => "F"
        };
    }

    /// <summary>Computed percentage for display.</summary>
    public decimal Percentage => Exam?.MaxScore > 0
        ? Math.Round(Score / Exam.MaxScore * 100, 1)
        : 0;
}
