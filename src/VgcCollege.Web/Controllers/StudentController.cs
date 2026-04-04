using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

/// <summary>
/// Student-only controller. Every query is filtered to the logged-in student's
/// own data – students cannot see other students' information.
/// Exam results are gated by Exam.ResultsReleased (server-side check).
/// </summary>
[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }

    // ── Helper: load this student's profile ───────────────────────────────────
    private async Task<StudentProfile?> GetMyProfileAsync()
    {
        var userId = _userManager.GetUserId(User);
        return await _db.StudentProfiles
            .FirstOrDefaultAsync(sp => sp.IdentityUserId == userId);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Dashboard
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Index()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound("Student profile not found for your account.");

        var vm = new StudentDashboardViewModel
        {
            Profile = await _db.StudentProfiles
                .Include(sp => sp.Enrolments).ThenInclude(e => e.Course).ThenInclude(c => c.Branch)
                .Include(sp => sp.AssignmentResults).ThenInclude(ar => ar.Assignment)
                .FirstAsync(sp => sp.Id == profile.Id)
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // My profile
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Profile()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound();
        return View(profile);
    }

    /// <summary>Students can edit their own contact info but not their student number.</summary>
    public async Task<IActionResult> EditProfile()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound();
        return View(profile);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(StudentProfile model)
    {
        var existing = await GetMyProfileAsync();
        if (existing == null) return NotFound();

        // Clear nav-property model state errors
        ModelState.Remove(nameof(StudentProfile.IdentityUser));
        ModelState.Remove(nameof(StudentProfile.Enrolments));
        ModelState.Remove(nameof(StudentProfile.AssignmentResults));
        ModelState.Remove(nameof(StudentProfile.ExamResults));

        if (!ModelState.IsValid) return View(model);

        // Copy editable fields only — Id, IdentityUserId, StudentNumber are immutable
        existing.Name = model.Name;
        existing.Email = model.Email;
        existing.Phone = model.Phone;
        existing.Address = model.Address;
        existing.DateOfBirth = model.DateOfBirth;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }
    // ═══════════════════════════════════════════════════════════════════════════
    // My enrolments + attendance
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> MyEnrolments()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound();

        var enrolments = await _db.CourseEnrolments
            .Where(e => e.StudentProfileId == profile.Id)
            .Include(e => e.Course).ThenInclude(c => c.Branch)
            .Include(e => e.AttendanceRecords)
            .OrderBy(e => e.Course.Name)
            .ToListAsync();

        return View(enrolments);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Gradebook (assignment results)
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Gradebook()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound();

        // Students always see their assignment results (no release flag on assignments)
        var results = await _db.AssignmentResults
            .Where(ar => ar.StudentProfileId == profile.Id)
            .Include(ar => ar.Assignment).ThenInclude(a => a.Course)
            .OrderBy(ar => ar.Assignment.Course.Name)
            .ThenBy(ar => ar.Assignment.DueDate)
            .ToListAsync();

        return View(results);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Exam results (gated by ResultsReleased)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows exam results for this student.
    /// KEY RULE: if Exam.ResultsReleased == false, the student sees a
    /// "Provisional – not yet released" message instead of their score.
    /// This check happens server-side in the query/view model – never rely on UI alone.
    /// </summary>
    public async Task<IActionResult> ExamResults()
    {
        var profile = await GetMyProfileAsync();
        if (profile == null) return NotFound();

        // Load all exam results for this student, including the exam's release flag
        var results = await _db.ExamResults
            .Where(er => er.StudentProfileId == profile.Id)
            .Include(er => er.Exam).ThenInclude(e => e.Course)
            .OrderBy(er => er.Exam.Course.Name)
            .ThenBy(er => er.Exam.Date)
            .ToListAsync();

        // Build view models that redact scores for unreleased exams
        var vms = results.Select(er => new StudentExamResultViewModel
        {
            ExamTitle     = er.Exam.Title,
            CourseName    = er.Exam.Course.Name,
            ExamDate      = er.Exam.Date,
            MaxScore      = er.Exam.MaxScore,
            // Server-side gate: only expose score & grade when released
            Score         = er.Exam.ResultsReleased ? er.Score : null,
            Grade         = er.Exam.ResultsReleased ? er.Grade : null,
            IsReleased    = er.Exam.ResultsReleased
        }).ToList();

        return View(vms);
    }
}

// ── View models ───────────────────────────────────────────────────────────────

public class StudentDashboardViewModel
{
    public StudentProfile Profile { get; set; } = null!;
}

/// <summary>
/// Wraps an exam result for the student view.
/// Score and Grade are nullable – null means "not yet released".
/// </summary>
public class StudentExamResultViewModel
{
    public string   ExamTitle  { get; set; } = string.Empty;
    public string   CourseName { get; set; } = string.Empty;
    public DateTime ExamDate   { get; set; }
    public decimal  MaxScore   { get; set; }
    public decimal? Score      { get; set; }  // null = provisional
    public string?  Grade      { get; set; }  // null = provisional
    public bool     IsReleased { get; set; }
}
