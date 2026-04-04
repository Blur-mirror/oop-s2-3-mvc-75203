using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VgcCollege.Web.Models;

/// <summary>
/// Represents a physical campus branch of VGC College.
/// </summary>
public class Branch
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    [Display(Name = "Branch Name")]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [ValidateNever]
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
