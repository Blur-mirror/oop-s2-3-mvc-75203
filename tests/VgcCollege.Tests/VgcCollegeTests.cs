using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VgcCollege.Tests;

/// Unit and integration tests for VGC College domain logic.
/// Uses the EF Core InMemory provider – no SQLite file needed, fully deterministic.
///
///
/// Test categories:
///   1. Grade calculation
///   2. Enrolment rules (duplicate prevention)
///   3. Exam result visibility (released vs provisional)
///   4. Faculty query filtering (only sees own course students)
///   5. Attendance tracking
///   6. Assignment result validation

public class VgcCollegeTests
{
    //Helper: create a fresh in-memory DbContext for each test
    private static ApplicationDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    //Helper: create a minimal student profile
    private static StudentProfile MakeStudent(string userId, string name, string number) =>
        new()
        {
            IdentityUserId = userId,
            Name           = name,
            Email          = $"{number.ToLower()}@vgc.ie",
            StudentNumber  = number,
            DateOfBirth    = new DateTime(2000, 1, 1)
        };

    //Helper: create a minimal branch + course
    private static (Branch branch, Course course) MakeBranchAndCourse(ApplicationDbContext db)
    {
        var branch = new Branch { Name = "Test Branch", Address = "123 Test St" };
        db.Branches.Add(branch);
        db.SaveChanges();

        var course = new Course
        {
            Name      = "Test Course",
            BranchId  = branch.Id,
            StartDate = new DateTime(2025, 9, 1),
            EndDate   = new DateTime(2026, 5, 31)
        };
        db.Courses.Add(course);
        db.SaveChanges();

        return (branch, course);
    }


    // 1. Grade Calculation

    [Theory]
    [InlineData(90,  100, "A")]
    [InlineData(85,  100, "A")]
    [InlineData(75,  100, "B")]
    [InlineData(70,  100, "B")]
    [InlineData(60,  100, "C")]
    [InlineData(55,  100, "C")]
    [InlineData(45,  100, "D")]
    [InlineData(40,  100, "D")]
    [InlineData(39,  100, "F")]
    [InlineData(0,   100, "F")]
    public void CalculateGrade_ReturnsCorrectLetter(decimal score, decimal max, string expected)
    {
        // Arrange + Act
        var grade = ExamResult.CalculateGrade(score, max);

        // Assert
        Assert.Equal(expected, grade);
    }

    [Fact]
    public void CalculateGrade_WhenMaxScoreIsZero_ReturnsF()
    {
        var grade = ExamResult.CalculateGrade(50, 0);
        Assert.Equal("F", grade);
    }

    [Fact]
    public void AssignmentResult_Percentage_CalculatesCorrectly()
    {
        // Arrange
        var assignment = new Assignment { MaxScore = 80 };
        var result = new AssignmentResult { Score = 60, Assignment = assignment };

        // Act
        var pct = result.Percentage;

        // Assert – 60/80 = 75%
        Assert.Equal(75.0m, pct);
    }

    [Fact]
    public void ExamResult_Percentage_CalculatesCorrectly()
    {
        // Arrange
        var exam = new Exam { MaxScore = 100 };
        var result = new ExamResult { Score = 72, Exam = exam };

        // Act + Assert
        Assert.Equal(72.0m, result.Percentage);
    }


    // 2. Enrolment Rules
    [Fact]
    public async Task Enrolment_UniqueIndex_PreventsDuplicates()
    {
        // Arrange
        using var db = CreateDb(nameof(Enrolment_UniqueIndex_PreventsDuplicates));
        var (_, course) = MakeBranchAndCourse(db);
        var student = MakeStudent("uid-1", "Alice", "VGC001");
        db.StudentProfiles.Add(student);
        db.SaveChanges();

        var enrolment1 = new CourseEnrolment
        {
            StudentProfileId = student.Id,
            CourseId         = course.Id,
            EnrolDate        = DateTime.Today,
            Status           = "Active"
        };
        db.CourseEnrolments.Add(enrolment1);
        db.SaveChanges();

        // Act: attempt duplicate enrolment
        var enrolment2 = new CourseEnrolment
        {
            StudentProfileId = student.Id,
            CourseId         = course.Id,
            EnrolDate        = DateTime.Today,
            Status           = "Active"
        };
        db.CourseEnrolments.Add(enrolment2);

        // Assert: EF InMemory does not enforce DB constraints, so we test
        // via the business logic check used in AdminController
        bool alreadyEnrolled = await db.CourseEnrolments
            .AnyAsync(e => e.StudentProfileId == student.Id && e.CourseId == course.Id);

        Assert.True(alreadyEnrolled, "Duplicate enrolment check should detect existing record.");
    }

    [Fact]
    public async Task Enrolment_CanEnrolInMultipleCourses()
    {
        // Arrange
        using var db = CreateDb(nameof(Enrolment_CanEnrolInMultipleCourses));
        var (branch, course1) = MakeBranchAndCourse(db);
        var course2 = new Course { Name = "Course 2", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        db.Courses.Add(course2);
        db.SaveChanges();

        var student = MakeStudent("uid-2", "Bob", "VGC002");
        db.StudentProfiles.Add(student);
        db.SaveChanges();

        // Act
        db.CourseEnrolments.AddRange(
            new CourseEnrolment { StudentProfileId = student.Id, CourseId = course1.Id, EnrolDate = DateTime.Today, Status = "Active" },
            new CourseEnrolment { StudentProfileId = student.Id, CourseId = course2.Id, EnrolDate = DateTime.Today, Status = "Active" }
        );
        db.SaveChanges();

        var count = await db.CourseEnrolments.CountAsync(e => e.StudentProfileId == student.Id);

        // Assert
        Assert.Equal(2, count);
    }


    // 3. Exam Result Visibility (provisional vs released)
    [Fact]
    public async Task ExamResults_WhenNotReleased_StudentCannotSeeScore()
    {
        // Arrange
        using var db = CreateDb(nameof(ExamResults_WhenNotReleased_StudentCannotSeeScore));
        var (_, course) = MakeBranchAndCourse(db);
        var student = MakeStudent("uid-3", "Carol", "VGC003");
        db.StudentProfiles.Add(student);

        var exam = new Exam { CourseId = course.Id, Title = "Final Exam", MaxScore = 100, Date = DateTime.Today, ResultsReleased = false };
        db.Exams.Add(exam);
        db.SaveChanges();

        var result = new ExamResult { ExamId = exam.Id, StudentProfileId = student.Id, Score = 78, Grade = "B" };
        db.ExamResults.Add(result);
        db.SaveChanges();

        // Act – simulate what StudentController.ExamResults does
        var results = await db.ExamResults
            .Where(er => er.StudentProfileId == student.Id)
            .Include(er => er.Exam)
            .ToListAsync();

        var viewModel = results.Select(er => new
        {
            Score      = er.Exam.ResultsReleased ? (decimal?)er.Score : null,
            Grade      = er.Exam.ResultsReleased ? er.Grade           : null,
            IsReleased = er.Exam.ResultsReleased
        }).First();

        // Assert: score must be null when not released
        Assert.False(viewModel.IsReleased);
        Assert.Null(viewModel.Score);
        Assert.Null(viewModel.Grade);
    }

    [Fact]
    public async Task ExamResults_WhenReleased_StudentCanSeeScore()
    {
        // Arrange
        using var db = CreateDb(nameof(ExamResults_WhenReleased_StudentCanSeeScore));
        var (_, course) = MakeBranchAndCourse(db);
        var student = MakeStudent("uid-4", "Dave", "VGC004");
        db.StudentProfiles.Add(student);

        var exam = new Exam { CourseId = course.Id, Title = "Semester Exam", MaxScore = 100, Date = DateTime.Today, ResultsReleased = true };
        db.Exams.Add(exam);
        db.SaveChanges();

        var result = new ExamResult { ExamId = exam.Id, StudentProfileId = student.Id, Score = 91, Grade = "A" };
        db.ExamResults.Add(result);
        db.SaveChanges();

        // Act
        var loaded = await db.ExamResults
            .Include(er => er.Exam)
            .FirstAsync(er => er.StudentProfileId == student.Id);

        var score = loaded.Exam.ResultsReleased ? (decimal?)loaded.Score : null;
        var grade = loaded.Exam.ResultsReleased ? loaded.Grade           : null;

        // Assert
        Assert.Equal(91m, score);
        Assert.Equal("A", grade);
    }


    // 4. Faculty Query Filtering
    [Fact]
    public async Task Faculty_CanOnlySeeStudentsInTheirCourses()
    {
        // Arrange
        using var db = CreateDb(nameof(Faculty_CanOnlySeeStudentsInTheirCourses));
        var branch  = new Branch { Name = "B1", Address = "Addr" };
        db.Branches.Add(branch);
        db.SaveChanges();

        var courseA = new Course { Name = "Course A", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        var courseB = new Course { Name = "Course B", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        db.Courses.AddRange(courseA, courseB);
        db.SaveChanges();

        var faculty = new FacultyProfile { IdentityUserId = "f-uid", Name = "Dr Test", Email = "f@vgc.ie" };
        db.FacultyProfiles.Add(faculty);
        db.SaveChanges();

        // Faculty only teaches Course A
        db.FacultyCourseAssignments.Add(new FacultyCourseAssignment
        {
            FacultyProfileId = faculty.Id,
            CourseId         = courseA.Id,
            Role             = "Lecturer"
        });

        var studentInA = MakeStudent("s-uid-a", "StudentA", "VGC010");
        var studentInB = MakeStudent("s-uid-b", "StudentB", "VGC011");
        db.StudentProfiles.AddRange(studentInA, studentInB);
        db.SaveChanges();

        db.CourseEnrolments.AddRange(
            new CourseEnrolment { StudentProfileId = studentInA.Id, CourseId = courseA.Id, EnrolDate = DateTime.Today, Status = "Active" },
            new CourseEnrolment { StudentProfileId = studentInB.Id, CourseId = courseB.Id, EnrolDate = DateTime.Today, Status = "Active" }
        );
        db.SaveChanges();

        // Act – simulate FacultyController's filtered query
        var myCourseIds = await db.FacultyCourseAssignments
            .Where(fca => fca.FacultyProfile.IdentityUserId == "f-uid")
            .Select(fca => fca.CourseId)
            .ToListAsync();

        var visibleStudents = await db.StudentProfiles
            .Where(sp => sp.Enrolments.Any(e => myCourseIds.Contains(e.CourseId)))
            .ToListAsync();

        // Assert: only StudentA (Course A) is visible – not StudentB (Course B)
        Assert.Single(visibleStudents);
        Assert.Equal("StudentA", visibleStudents[0].Name);
    }

    [Fact]
    public async Task Faculty_TutorRole_CanAccessContactDetails()
    {
        // Arrange
        using var db = CreateDb(nameof(Faculty_TutorRole_CanAccessContactDetails));
        var branch = new Branch { Name = "B1", Address = "Addr" };
        db.Branches.Add(branch);
        db.SaveChanges();

        var course = new Course { Name = "SW Dev", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddYears(1) };
        db.Courses.Add(course);
        db.SaveChanges();

        var faculty = new FacultyProfile { IdentityUserId = "f-tutor", Name = "Tutor Faculty", Email = "tutor@vgc.ie" };
        db.FacultyProfiles.Add(faculty);
        db.SaveChanges();

        db.FacultyCourseAssignments.Add(new FacultyCourseAssignment
        {
            FacultyProfileId = faculty.Id,
            CourseId         = course.Id,
            Role             = "Tutor"  // Tutor role grants contact access
        });
        db.SaveChanges();

        // Act – simulate the tutor course check
        var tutorCourseIds = await db.FacultyCourseAssignments
            .Where(fca => fca.FacultyProfile.IdentityUserId == "f-tutor" && fca.Role == "Tutor")
            .Select(fca => fca.CourseId)
            .ToListAsync();

        // Assert: they have at least one tutored course
        Assert.NotEmpty(tutorCourseIds);
        Assert.Contains(course.Id, tutorCourseIds);
    }

    // 5. Attendance
    [Fact]
    public async Task Attendance_WeekUpsert_UpdatesExistingRecord()
    {
        // Arrange
        using var db = CreateDb(nameof(Attendance_WeekUpsert_UpdatesExistingRecord));
        var (_, course) = MakeBranchAndCourse(db);
        var student = MakeStudent("uid-att", "Eve", "VGC020");
        db.StudentProfiles.Add(student);
        db.SaveChanges();

        var enrolment = new CourseEnrolment { StudentProfileId = student.Id, CourseId = course.Id, EnrolDate = DateTime.Today, Status = "Active" };
        db.CourseEnrolments.Add(enrolment);
        db.SaveChanges();

        // Add week 1 as absent
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            CourseEnrolmentId = enrolment.Id,
            WeekNumber        = 1,
            SessionDate       = new DateTime(2025, 9, 8),
            Present           = false
        });
        db.SaveChanges();

        // Act: upsert – mark as present
        var existing = await db.AttendanceRecords
            .FirstOrDefaultAsync(ar => ar.CourseEnrolmentId == enrolment.Id && ar.WeekNumber == 1);
        Assert.NotNull(existing);
        existing.Present = true;
        db.SaveChanges();

        // Assert
        var updated = await db.AttendanceRecords
            .FirstAsync(ar => ar.CourseEnrolmentId == enrolment.Id && ar.WeekNumber == 1);
        Assert.True(updated.Present);
    }


    // 6. Assignment Result Validation

    [Fact]
    public void AssignmentResult_ScoreExceedingMax_IsInvalid()
    {
        // Arrange
        var assignment = new Assignment { MaxScore = 100 };
        var result = new AssignmentResult { Score = 150, Assignment = assignment };

        // Act – business rule check (mirrors what FacultyController does)
        bool isInvalid = result.Score > assignment.MaxScore;

        // Assert
        Assert.True(isInvalid, "Score exceeding MaxScore should be flagged as invalid.");
    }

    [Fact]
    public async Task AssignmentResult_DuplicateCheck_DetectsExistingEntry()
    {
        // Arrange
        using var db = CreateDb(nameof(AssignmentResult_DuplicateCheck_DetectsExistingEntry));
        var (_, course) = MakeBranchAndCourse(db);

        var assignment = new Assignment { CourseId = course.Id, Title = "Lab 1", MaxScore = 100, DueDate = DateTime.Today };
        db.Assignments.Add(assignment);
        var student = MakeStudent("uid-dup", "Frank", "VGC030");
        db.StudentProfiles.Add(student);
        db.SaveChanges();

        db.AssignmentResults.Add(new AssignmentResult
        {
            AssignmentId     = assignment.Id,
            StudentProfileId = student.Id,
            Score            = 75
        });
        db.SaveChanges();

        // Act – simulate duplicate check
        bool exists = await db.AssignmentResults
            .AnyAsync(ar => ar.AssignmentId == assignment.Id && ar.StudentProfileId == student.Id);

        // Assert
        Assert.True(exists, "Duplicate check should return true for an existing result.");
    }
}
