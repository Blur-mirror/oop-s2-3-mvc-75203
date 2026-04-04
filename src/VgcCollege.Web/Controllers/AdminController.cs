using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;



namespace VgcCollege.Web.Controllers;


/// Admin-only controller. Every action requires the Administrator role
/// (enforced server-side via [Authorize] – not just hidden UI links).
/// Covers: branches, courses, faculty assignments, enrolments, result release.
[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db          = db;
        _userManager = userManager;
    }


    // Dashboard
    public async Task<IActionResult> Index()
    {
        var vm = new AdminDashboardViewModel
        {
            BranchCount   = await _db.Branches.CountAsync(),
            CourseCount   = await _db.Courses.CountAsync(),
            StudentCount  = await _db.StudentProfiles.CountAsync(),
            FacultyCount  = await _db.FacultyProfiles.CountAsync(),
            EnrolmentCount = await _db.CourseEnrolments.CountAsync(),
            RecentEnrolments = await _db.CourseEnrolments
                .Include(e => e.StudentProfile)
                .Include(e => e.Course)
                .OrderByDescending(e => e.EnrolDate)
                .Take(5)
                .ToListAsync()
        };
        return View(vm);
    }


    // Branches
    public async Task<IActionResult> Branches() =>
        View(await _db.Branches.Include(b => b.Courses).ToListAsync());

    public IActionResult CreateBranch() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranch(Branch model)
    {
        if (!ModelState.IsValid) return View(model);
        _db.Branches.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Branch '{model.Name}' created.";
        return RedirectToAction(nameof(Branches));
    }

    public async Task<IActionResult> EditBranch(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();
        return View(branch);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBranch(int id, Branch model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Branch updated.";
        return RedirectToAction(nameof(Branches));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        var branch = await _db.Branches.FindAsync(id);
        if (branch == null) return NotFound();

        // Guard: don't delete a branch that still has courses
        bool hasCourses = await _db.Courses.AnyAsync(c => c.BranchId == id);
        if (hasCourses)
        {
            TempData["Error"] = "Cannot delete a branch that still has courses assigned.";
            return RedirectToAction(nameof(Branches));
        }

        _db.Branches.Remove(branch);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Branch deleted.";
        return RedirectToAction(nameof(Branches));
    }


    // Courses
    public async Task<IActionResult> Courses() =>
        View(await _db.Courses
            .Include(c => c.Branch)
            .Include(c => c.FacultyAssignments).ThenInclude(fa => fa.FacultyProfile)
            .OrderBy(c => c.Branch.Name).ThenBy(c => c.Name)
            .ToListAsync());

    public async Task<IActionResult> CreateCourse()
    {
        await PopulateBranchSelectList();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCourse(Course model)
    {
        if (!ModelState.IsValid) { await PopulateBranchSelectList(); return View(model); }
        _db.Courses.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Course '{model.Name}' created.";
        return RedirectToAction(nameof(Courses));
    }

    public async Task<IActionResult> EditCourse(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        await PopulateBranchSelectList(course.BranchId);
        return View(course);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCourse(int id, Course model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) { await PopulateBranchSelectList(model.BranchId); return View(model); }
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course updated.";
        return RedirectToAction(nameof(Courses));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course deleted.";
        return RedirectToAction(nameof(Courses));
    }


    // Students
    public async Task<IActionResult> Students() =>
        View(await _db.StudentProfiles
            .Include(s => s.Enrolments).ThenInclude(e => e.Course)
            .OrderBy(s => s.Name)
            .ToListAsync());

    public async Task<IActionResult> StudentDetails(int id)
    {
        var student = await _db.StudentProfiles
            .Include(s => s.Enrolments).ThenInclude(e => e.Course).ThenInclude(c => c.Branch)
            .Include(s => s.Enrolments).ThenInclude(e => e.AttendanceRecords)
            .Include(s => s.AssignmentResults).ThenInclude(ar => ar.Assignment).ThenInclude(a => a.Course)
            .Include(s => s.ExamResults).ThenInclude(er => er.Exam).ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();
        return View(student);
    }

    public async Task<IActionResult> EditStudent(int id)
    {
        var student = await _db.StudentProfiles.FindAsync(id);
        if (student == null) return NotFound();
        return View(student);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStudent(int id, StudentProfile model)
    {
        if (id != model.Id) return BadRequest();
        // Preserve Identity user ID – never allow it to be changed via form
        var existing = await _db.StudentProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (existing == null) return NotFound();
        model.IdentityUserId = existing.IdentityUserId;

        if (!ModelState.IsValid) return View(model);
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student profile updated.";
        return RedirectToAction(nameof(Students));
    }

    // Faculty management
    public async Task<IActionResult> Faculty() =>
        View(await _db.FacultyProfiles
            .Include(f => f.CourseAssignments).ThenInclude(ca => ca.Course)
            .OrderBy(f => f.Name)
            .ToListAsync());

    ///Assign a faculty member to a course with a role
    public async Task<IActionResult> AssignFaculty()
    {
        await PopulateFacultySelectList();
        await PopulateCourseSelectList();
        return View(new FacultyCourseAssignment());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignFaculty(FacultyCourseAssignment model)
    {
        // Check for duplicate assignment
        bool exists = await _db.FacultyCourseAssignments
            .AnyAsync(fca => fca.FacultyProfileId == model.FacultyProfileId
                          && fca.CourseId         == model.CourseId);
        if (exists)
        {
            ModelState.AddModelError("", "This faculty member is already assigned to that course.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateFacultySelectList(model.FacultyProfileId);
            await PopulateCourseSelectList(model.CourseId);
            return View(model);
        }

        _db.FacultyCourseAssignments.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Faculty assigned to course.";
        return RedirectToAction(nameof(Faculty));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFacultyAssignment(int id)
    {
        var assignment = await _db.FacultyCourseAssignments.FindAsync(id);
        if (assignment != null)
        {
            _db.FacultyCourseAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "Assignment removed.";
        return RedirectToAction(nameof(Faculty));
    }


    // Enrolments
    public async Task<IActionResult> Enrolments(int? courseId)
    {
        var query = _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course).ThenInclude(c => c.Branch)
            .AsQueryable();

        if (courseId.HasValue)
            query = query.Where(e => e.CourseId == courseId.Value);

        await PopulateCourseSelectList(courseId);
        return View(await query.OrderBy(e => e.Course.Name).ThenBy(e => e.StudentProfile.Name).ToListAsync());
    }

    public async Task<IActionResult> CreateEnrolment()
    {
        await PopulateStudentSelectList();
        await PopulateCourseSelectList();
        return View(new CourseEnrolment { EnrolDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEnrolment(CourseEnrolment model)
    {
        // Prevent duplicate enrolment
        bool alreadyEnrolled = await _db.CourseEnrolments
            .AnyAsync(e => e.StudentProfileId == model.StudentProfileId
                        && e.CourseId         == model.CourseId);
        if (alreadyEnrolled)
            ModelState.AddModelError("", "This student is already enrolled in that course.");

        if (!ModelState.IsValid)
        {
            await PopulateStudentSelectList(model.StudentProfileId);
            await PopulateCourseSelectList(model.CourseId);
            return View(model);
        }

        _db.CourseEnrolments.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student enrolled successfully.";
        return RedirectToAction(nameof(Enrolments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEnrolmentStatus(int id, string status)
    {
        var enrolment = await _db.CourseEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        enrolment.Status = status;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Enrolment status updated to '{status}'.";
        return RedirectToAction(nameof(Enrolments));
    }


    // Exams – result release management
    public async Task<IActionResult> Exams() =>
        View(await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.Results)
            .OrderBy(e => e.Course.Name).ThenBy(e => e.Date)
            .ToListAsync());


    /// Toggles the ResultsReleased flag. This is the gating mechanism
    /// that controls whether students can see their exam results.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleExamRelease(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam == null) return NotFound();
        exam.ResultsReleased = !exam.ResultsReleased;
        await _db.SaveChangesAsync();
        TempData["Success"] = exam.ResultsReleased
            ? $"Results for '{exam.Title}' are now RELEASED to students."
            : $"Results for '{exam.Title}' are now PROVISIONAL (hidden from students).";
        return RedirectToAction(nameof(Exams));
    }

    public async Task<IActionResult> CreateExam()
    {
        await PopulateCourseSelectList();
        return View(new Exam { Date = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateExam(Exam model)
    {
        if (!ModelState.IsValid) { await PopulateCourseSelectList(model.CourseId); return View(model); }
        _db.Exams.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Exam '{model.Title}' created.";
        return RedirectToAction(nameof(Exams));
    }

    public async Task<IActionResult> EditExam(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam == null) return NotFound();
        await PopulateCourseSelectList(exam.CourseId);
        return View(exam);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExam(int id, Exam model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) { await PopulateCourseSelectList(model.CourseId); return View(model); }
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam updated.";
        return RedirectToAction(nameof(Exams));
    }


    // Assignments (admin view)
    public async Task<IActionResult> Assignments() =>
        View(await _db.Assignments
            .Include(a => a.Course)
            .Include(a => a.Results)
            .OrderBy(a => a.Course.Name).ThenBy(a => a.DueDate)
            .ToListAsync());

    public async Task<IActionResult> CreateAssignment()
    {
        await PopulateCourseSelectList();
        return View(new Assignment { DueDate = DateTime.Today.AddDays(14) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssignment(Assignment model)
    {
        if (!ModelState.IsValid) { await PopulateCourseSelectList(model.CourseId); return View(model); }
        _db.Assignments.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Assignment '{model.Title}' created.";
        return RedirectToAction(nameof(Assignments));
    }

    public async Task<IActionResult> EditAssignment(int id)
    {
        var assignment = await _db.Assignments.FindAsync(id);
        if (assignment == null) return NotFound();
        await PopulateCourseSelectList(assignment.CourseId);
        return View(assignment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAssignment(int id, Assignment model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) { await PopulateCourseSelectList(model.CourseId); return View(model); }
        _db.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Assignment updated.";
        return RedirectToAction(nameof(Assignments));
    }


    // Private helpers
    private async Task PopulateBranchSelectList(int? selectedId = null)
    {
        ViewBag.BranchId = new SelectList(
            await _db.Branches.OrderBy(b => b.Name).ToListAsync(),
            "Id", "Name", selectedId);
    }

    private async Task PopulateCourseSelectList(int? selectedId = null)
    {
        ViewBag.CourseId = new SelectList(
            await _db.Courses.Include(c => c.Branch)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, Name = $"{c.Name} ({c.Branch.Name})" })
                .ToListAsync(),
            "Id", "Name", selectedId);
    }

    private async Task PopulateStudentSelectList(int? selectedId = null)
    {
        ViewBag.StudentProfileId = new SelectList(
            await _db.StudentProfiles.OrderBy(s => s.Name)
                .Select(s => new { s.Id, Display = $"{s.Name} ({s.StudentNumber})" })
                .ToListAsync(),
            "Id", "Display", selectedId);
    }

    private async Task PopulateFacultySelectList(int? selectedId = null)
    {
        ViewBag.FacultyProfileId = new SelectList(
            await _db.FacultyProfiles.OrderBy(f => f.Name).ToListAsync(),
            "Id", "Name", selectedId);
    }
}

//Admin dashboard view model
public class AdminDashboardViewModel
{
    public int BranchCount    { get; set; }
    public int CourseCount    { get; set; }
    public int StudentCount   { get; set; }
    public int FacultyCount   { get; set; }
    public int EnrolmentCount { get; set; }
    public List<CourseEnrolment> RecentEnrolments { get; set; } = new();
}
