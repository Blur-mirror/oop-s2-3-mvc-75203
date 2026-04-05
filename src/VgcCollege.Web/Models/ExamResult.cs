using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;


/// The score and grade a student received in an exam.
/// Visibility is gated by Exam.ResultsReleased – enforced server-side.
public class ExamResult
{
    public int Id { get; set; }

    public int ExamId { get; set; }
    [ValidateNever]
    public Exam Exam { get; set; } = null!;

    public int StudentProfileId { get; set; }
    [ValidateNever]
    public StudentProfile StudentProfile { get; set; } = null!;

    [Range(0, 10000)]
    public decimal Score { get; set; }

    [MaxLength(2)]
    public string Grade { get; set; } = string.Empty;

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

    public decimal Percentage => Exam?.MaxScore > 0
        ? Math.Round(Score / Exam.MaxScore * 100, 1)
        : 0;
}
