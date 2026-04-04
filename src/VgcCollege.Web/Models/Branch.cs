using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

/// <summary>
/// Represents a physical campus branch of VGC College.
/// Courses are offered at a specific branch.
/// </summary>
public class Branch
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    [Display(Name = "Branch Name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    // ── Navigation ──────────────────────────────────────────────────────────
    /// <summary>All courses offered at this branch.</summary>
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
