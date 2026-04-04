using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

/// <summary>
/// Faculty-only controller.
/// All queries are filtered server-side to this faculty member's courses only.
/// Contact details are further restricted to "Tutor"-role courses.
/// </summary>
[Authorize(Roles = "Faculty")]
public class FacultyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public FacultyController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<FacultyProfile?> GetCurrentFacultyAsync()
    {
        var userId = _userManager.GetUserId(User);
        return await _db.FacultyProfiles
            .Include(f => f.CourseAssignments).ThenInclude(ca => ca.Course)
            .FirstOrDefaultAsync(f => f.IdentityUserId == userId);
    }

    /// <summary>Returns IDs of all courses this faculty member teaches.</summary>
    private async Task<List<int>> GetMyCourseIdsAsync()
    {
        var userId = _userManager.GetUserId(User);
        return await _db.FacultyCourseAssignments
            .Where(fca => fca.FacultyProfile.IdentityUserId == userId)
            .Select(fca => fca.CourseId)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Dashboard
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Index()
    {
        var faculty = await GetCurrentFacultyAsync();
        if (faculty == null) return NotFound("Faculty profile not found for this account.");

        var courseIds = faculty.CourseAssignments.Select(ca => ca.CourseId).ToList();

        var vm = new FacultyDashboardViewModel
        {
            FacultyProfile = faculty,
            MyCourses = await _db.Courses
                .Where(c => courseIds.Contains(c.Id))
                .Include(c => c.Branch)
                .Include(c => c.Enrolments)
                .ToListAsync(),
            // Recent results scoped to this faculty's courses
            RecentResults = await _db.AssignmentResults
                .Where(ar => courseIds.Contains(ar.Assignment.CourseId))
                .Include(ar => ar.Assignment)
                .Include(ar => ar.StudentProfile)
                .OrderByDescending(ar => ar.Id)
                .Take(5)
                .ToListAsync()
        };
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Courses & Students
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> MyCourses()
    {
        var courseIds = await GetMyCourseIdsAsync();
        var courses = await _db.Courses
            .Where(c => courseIds.Contains(c.Id))
            .Include(c => c.Branch)
            .Include(c => c.Enrolments).ThenInclude(e => e.StudentProfile)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(courses);
    }

    /// <summary>
    /// Lists students enrolled in a specific course taught by this faculty.
    /// Returns 403 if the course isn't theirs.
    /// </summary>
    public async Task<IActionResult> CourseStudents(int courseId)
    {
        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(courseId)) return Forbid();

        var course = await _db.Courses
            .Include(c => c.Branch)
            .Include(c => c.Enrolments).ThenInclude(e => e.StudentProfile)
            .Include(c => c.Enrolments).ThenInclude(e => e.AttendanceRecords)
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course == null) return NotFound();
        return View(course);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Student contact details (Tutor role only)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows contact details ONLY for students in courses where this faculty
    /// has the "Tutor" role. Server-side enforced.
    /// </summary>
    public async Task<IActionResult> StudentContacts()
    {
        var userId = _userManager.GetUserId(User);

        var tutorCourseIds = await _db.FacultyCourseAssignments
            .Where(fca => fca.FacultyProfile.IdentityUserId == userId && fca.Role == "Tutor")
            .Select(fca => fca.CourseId)
            .ToListAsync();

        var students = await _db.StudentProfiles
            .Where(sp => sp.Enrolments.Any(e => tutorCourseIds.Contains(e.CourseId)))
            .Include(sp => sp.Enrolments).ThenInclude(e => e.Course)
            .OrderBy(sp => sp.Name)
            .ToListAsync();

        ViewBag.TutorCourseIds = tutorCourseIds;
        return View(students);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Gradebook (assignment results)
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Gradebook(int? courseId)
    {
        var courseIds = await GetMyCourseIdsAsync();

        if (courseId.HasValue && !courseIds.Contains(courseId.Value))
            return Forbid();

        var filterIds = courseId.HasValue ? new[] { courseId.Value } : courseIds.ToArray();

        var results = await _db.AssignmentResults
            .Where(ar => filterIds.Contains(ar.Assignment.CourseId))
            .Include(ar => ar.Assignment).ThenInclude(a => a.Course)
            .Include(ar => ar.StudentProfile)
            .OrderBy(ar => ar.Assignment.Course.Name)
            .ThenBy(ar => ar.Assignment.Title)
            .ThenBy(ar => ar.StudentProfile.Name)
            .ToListAsync();

        var myCourses = await _db.Courses
            .Where(c => courseIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.CourseId = new SelectList(myCourses, "Id", "Name", courseId);
        ViewBag.SelectedCourseId = courseId;
        return View(results);
    }

    public async Task<IActionResult> AddResult(int? assignmentId)
    {
        var courseIds = await GetMyCourseIdsAsync();
        await PopulateAssignmentSelectList(courseIds, assignmentId);
        // FIX: scope students to only those enrolled in this faculty's courses
        await PopulateStudentSelectListForCourses(courseIds);
        return View(new AssignmentResult());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResult(AssignmentResult model)
    {
        var courseIds = await GetMyCourseIdsAsync();

        var assignment = await _db.Assignments.FindAsync(model.AssignmentId);
        if (assignment == null || !courseIds.Contains(assignment.CourseId))
            return Forbid();

        // Round score to 2dp immediately so we never store long decimals
        model.Score = Math.Round(model.Score, 2);

        if (model.Score > assignment.MaxScore)
            ModelState.AddModelError("Score", $"Score cannot exceed the max of {assignment.MaxScore}.");

        bool exists = await _db.AssignmentResults
            .AnyAsync(ar => ar.AssignmentId == model.AssignmentId
                         && ar.StudentProfileId == model.StudentProfileId);
        if (exists)
            ModelState.AddModelError("", "A result for this student already exists. Use Edit instead.");

        // FIX: verify the student is actually enrolled in this assignment's course
        bool studentEnrolled = await _db.CourseEnrolments
            .AnyAsync(e => e.StudentProfileId == model.StudentProfileId
                        && e.CourseId == assignment.CourseId);
        if (!studentEnrolled)
            ModelState.AddModelError("StudentProfileId", "This student is not enrolled in the assignment's course.");

        if (!ModelState.IsValid)
        {
            await PopulateAssignmentSelectList(courseIds, model.AssignmentId);
            await PopulateStudentSelectListForCourses(courseIds);
            return View(model);
        }

        _db.AssignmentResults.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Result saved.";
        return RedirectToAction(nameof(Gradebook));
    }

    public async Task<IActionResult> EditResult(int id)
    {
        var result = await _db.AssignmentResults
            .Include(r => r.Assignment)
            .Include(r => r.StudentProfile)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (result == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(result.Assignment.CourseId)) return Forbid();

        return View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResult(int id, AssignmentResult model)
    {
        if (id != model.Id) return BadRequest();

        var existing = await _db.AssignmentResults
            .Include(r => r.Assignment)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (existing == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(existing.Assignment.CourseId)) return Forbid();

        // Round to 2dp
        model.Score = Math.Round(model.Score, 2);

        if (model.Score > existing.Assignment.MaxScore)
            ModelState.AddModelError("Score", $"Score cannot exceed {existing.Assignment.MaxScore}.");

        if (!ModelState.IsValid)
        {
            model.Assignment     = existing.Assignment;
            model.StudentProfile = await _db.StudentProfiles.FindAsync(existing.StudentProfileId)
                                   ?? new StudentProfile();
            return View(model);
        }

        existing.Score    = model.Score;
        existing.Feedback = model.Feedback;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Result updated.";
        return RedirectToAction(nameof(Gradebook));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Exam Results
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> ExamResults(int? examId)
    {
        var courseIds = await GetMyCourseIdsAsync();

        if (examId.HasValue)
        {
            var exam = await _db.Exams.FindAsync(examId.Value);
            if (exam == null || !courseIds.Contains(exam.CourseId)) return Forbid();
        }

        var results = await _db.ExamResults
            .Where(er => courseIds.Contains(er.Exam.CourseId)
                      && (!examId.HasValue || er.ExamId == examId.Value))
            .Include(er => er.Exam).ThenInclude(e => e.Course)
            .Include(er => er.StudentProfile)
            .OrderBy(er => er.Exam.Title).ThenBy(er => er.StudentProfile.Name)
            .ToListAsync();

        var myExams = await _db.Exams
            .Where(e => courseIds.Contains(e.CourseId))
            .OrderBy(e => e.Title)
            .ToListAsync();

        ViewBag.ExamId = new SelectList(myExams, "Id", "Title", examId);
        return View(results);
    }

    public async Task<IActionResult> AddExamResult(int? examId)
    {
        var courseIds = await GetMyCourseIdsAsync();
        await PopulateExamSelectList(courseIds, examId);
        await PopulateStudentSelectListForCourses(courseIds);
        return View(new ExamResult());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExamResult(ExamResult model)
    {
        var courseIds = await GetMyCourseIdsAsync();
        var exam = await _db.Exams.FindAsync(model.ExamId);
        if (exam == null || !courseIds.Contains(exam.CourseId)) return Forbid();

        model.Score = Math.Round(model.Score, 2);

        if (model.Score > exam.MaxScore)
            ModelState.AddModelError("Score", $"Score cannot exceed {exam.MaxScore}.");

        bool exists = await _db.ExamResults
            .AnyAsync(er => er.ExamId == model.ExamId
                         && er.StudentProfileId == model.StudentProfileId);
        if (exists)
            ModelState.AddModelError("", "A result for this student already exists.");

        bool studentEnrolled = await _db.CourseEnrolments
            .AnyAsync(e => e.StudentProfileId == model.StudentProfileId
                        && e.CourseId == exam.CourseId);
        if (!studentEnrolled)
            ModelState.AddModelError("StudentProfileId", "This student is not enrolled in the exam's course.");

        if (!ModelState.IsValid)
        {
            await PopulateExamSelectList(courseIds, model.ExamId);
            await PopulateStudentSelectListForCourses(courseIds);
            return View(model);
        }

        model.Grade = ExamResult.CalculateGrade(model.Score, exam.MaxScore);
        _db.ExamResults.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam result saved.";
        return RedirectToAction(nameof(ExamResults));
    }

    public async Task<IActionResult> EditExamResult(int id)
    {
        var result = await _db.ExamResults
            .Include(r => r.Exam)
            .Include(r => r.StudentProfile)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (result == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(result.Exam.CourseId)) return Forbid();

        return View(result);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExamResult(int id, ExamResult model)
    {
        if (id != model.Id) return BadRequest();

        var existing = await _db.ExamResults
            .Include(r => r.Exam)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (existing == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(existing.Exam.CourseId)) return Forbid();

        model.Score = Math.Round(model.Score, 2);

        if (model.Score > existing.Exam.MaxScore)
            ModelState.AddModelError("Score", $"Score cannot exceed {existing.Exam.MaxScore}.");

        if (!ModelState.IsValid)
        {
            model.Exam           = existing.Exam;
            model.StudentProfile = await _db.StudentProfiles.FindAsync(existing.StudentProfileId)
                                   ?? new StudentProfile();
            return View(model);
        }

        existing.Score = model.Score;
        existing.Grade = ExamResult.CalculateGrade(model.Score, existing.Exam.MaxScore);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam result updated.";
        return RedirectToAction(nameof(ExamResults));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Attendance
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Attendance(int enrolmentId)
    {
        var enrolment = await _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course)
            .Include(e => e.AttendanceRecords.OrderBy(a => a.WeekNumber))
            .FirstOrDefaultAsync(e => e.Id == enrolmentId);

        if (enrolment == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(enrolment.CourseId)) return Forbid();

        return View(enrolment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAttendance(int enrolmentId, int weekNumber,
        DateTime sessionDate, bool present)
    {
        var enrolment = await _db.CourseEnrolments.FindAsync(enrolmentId);
        if (enrolment == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(enrolment.CourseId)) return Forbid();

        // Upsert: update existing week record or add new one
        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(ar => ar.CourseEnrolmentId == enrolmentId
                                    && ar.WeekNumber == weekNumber);
        if (existing != null)
        {
            existing.Present     = present;
            existing.SessionDate = sessionDate;
        }
        else
        {
            _db.AttendanceRecords.Add(new AttendanceRecord
            {
                CourseEnrolmentId = enrolmentId,
                WeekNumber        = weekNumber,
                SessionDate       = sessionDate,
                Present           = present
            });
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Week {weekNumber} attendance saved.";
        return RedirectToAction(nameof(Attendance), new { enrolmentId });
    }

    /// <summary>
    /// Deletes a single attendance record so faculty can correct mistakes.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttendance(int id, int enrolmentId)
    {
        var record = await _db.AttendanceRecords.FindAsync(id);
        if (record == null) return NotFound();

        var enrolment = await _db.CourseEnrolments.FindAsync(record.CourseEnrolmentId);
        if (enrolment == null) return NotFound();

        var courseIds = await GetMyCourseIdsAsync();
        if (!courseIds.Contains(enrolment.CourseId)) return Forbid();

        _db.AttendanceRecords.Remove(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Week {record.WeekNumber} record deleted.";
        return RedirectToAction(nameof(Attendance), new { enrolmentId });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task PopulateAssignmentSelectList(List<int> courseIds, int? selectedId = null)
    {
        ViewBag.AssignmentId = new SelectList(
            await _db.Assignments
                .Where(a => courseIds.Contains(a.CourseId))
                .Include(a => a.Course)
                .OrderBy(a => a.Course.Name).ThenBy(a => a.Title)
                .Select(a => new { a.Id, Display = $"{a.Title} ({a.Course.Name})" })
                .ToListAsync(),
            "Id", "Display", selectedId);
    }

    /// <summary>
    /// Scopes student dropdown to students enrolled in THIS faculty's courses only.
    /// Fixes the bug where all students appeared regardless of their enrolment.
    /// </summary>
    private async Task PopulateStudentSelectListForCourses(List<int> courseIds, int? selectedId = null)
    {
        // Only students who are enrolled in at least one of this faculty's courses
        var students = await _db.StudentProfiles
            .Where(sp => sp.Enrolments.Any(e => courseIds.Contains(e.CourseId)))
            .OrderBy(sp => sp.Name)
            .Select(sp => new { sp.Id, Display = $"{sp.Name} ({sp.StudentNumber})" })
            .ToListAsync();

        ViewBag.StudentProfileId = new SelectList(students, "Id", "Display", selectedId);
    }

    private async Task PopulateExamSelectList(List<int> courseIds, int? selectedId = null)
    {
        ViewBag.ExamId = new SelectList(
            await _db.Exams
                .Where(e => courseIds.Contains(e.CourseId))
                .Include(e => e.Course)
                .OrderBy(e => e.Course.Name).ThenBy(e => e.Title)
                .Select(e => new { e.Id, Display = $"{e.Title} ({e.Course.Name})" })
                .ToListAsync(),
            "Id", "Display", selectedId);
    }
}

// ── Faculty dashboard view model ──────────────────────────────────────────────
public class FacultyDashboardViewModel
{
    public FacultyProfile FacultyProfile { get; set; } = null!;
    public List<Course> MyCourses { get; set; } = new();
    public List<AssignmentResult> RecentResults { get; set; } = new();
}
