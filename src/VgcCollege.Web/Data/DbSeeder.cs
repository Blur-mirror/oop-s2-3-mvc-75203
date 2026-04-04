using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Data;

/// <summary>
/// Runs once at startup to populate an empty database with roles, demo accounts,
/// and realistic Bogus-generated content. Scores are rounded to 2 decimal places.
///
/// Seeded credentials:
///   admin@vgc.ie      / Admin1234!
///   faculty1@vgc.ie   / Faculty1234!
///   faculty2@vgc.ie   / Faculty1234!
///   student1@vgc.ie   / Student1234!
///   student2@vgc.ie   / Student1234!
///   student3@vgc.ie   / Student1234!
///   student4@vgc.ie   / Student1234!
/// </summary>
public static class DbSeeder
{
    private const int SeedValue = 42;

    public static async Task EnsureSeeded(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        // Guard: skip if already seeded
        if (await db.Branches.AnyAsync()) return;

        // ── 1. Roles ──────────────────────────────────────────────────────────
        foreach (var role in new[] { "Administrator", "Faculty", "Student" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // ── 2. Admin ──────────────────────────────────────────────────────────
        var admin = new ApplicationUser
        {
            UserName = "admin@vgc.ie",
            Email = "admin@vgc.ie",
            DisplayName = "System Administrator",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "Admin1234!");
        await userManager.AddToRoleAsync(admin, "Administrator");

        // ── 3. Branches ───────────────────────────────────────────────────────
        var branches = new List<Branch>
        {
            new() { Name = "VGC Dublin City Campus",  Address = "12 O'Connell Street, Dublin 1" },
            new() { Name = "VGC Cork South Campus",   Address = "45 Patrick Street, Cork" },
            new() { Name = "VGC Galway West Campus",  Address = "78 Shop Street, Galway" }
        };
        db.Branches.AddRange(branches);
        await db.SaveChangesAsync();

        // ── 4. Courses ────────────────────────────────────────────────────────
        var courseData = new[]
        {
            ("Software Development",       0),
            ("Data Science & Analytics",   0),
            ("Cybersecurity Fundamentals", 1),
            ("Network Engineering",        1),
            ("Digital Marketing",          2),
            ("Business Computing",         2)
        };
        var courses = courseData.Select(cd => new Course
        {
            Name = cd.Item1,
            BranchId = branches[cd.Item2].Id,
            StartDate = new DateTime(2025, 9, 1),
            EndDate = new DateTime(2026, 5, 31)
        }).ToList();
        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();

        // ── 5. Faculty ────────────────────────────────────────────────────────
        var facultyData = new[]
        {
            ("faculty1@vgc.ie", "Dr. Sarah O'Brien",  "+353 1 234 5678"),
            ("faculty2@vgc.ie", "Prof. James Murphy", "+353 21 456 7890")
        };
        var facultyProfiles = new List<FacultyProfile>();
        foreach (var (email, name, phone) in facultyData)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = name,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Faculty1234!");
            await userManager.AddToRoleAsync(user, "Faculty");
            facultyProfiles.Add(new FacultyProfile
            {
                IdentityUserId = user.Id,
                Name = name,
                Email = email,
                Phone = phone
            });
        }
        db.FacultyProfiles.AddRange(facultyProfiles);
        await db.SaveChangesAsync();

        // ── 6. Faculty assignments ────────────────────────────────────────────
        // faculty1 → Software Dev (Tutor), Data Science, Cybersecurity
        // faculty2 → Network Eng (Tutor), Digital Marketing, Business Computing
        db.FacultyCourseAssignments.AddRange(
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[0].Id, Role = "Tutor" },
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[1].Id, Role = "Lecturer" },
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[2].Id, Role = "Lecturer" },
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[3].Id, Role = "Tutor" },
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[4].Id, Role = "Lecturer" },
            new FacultyCourseAssignment { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[5].Id, Role = "Lecturer" }
        );
        await db.SaveChangesAsync();

        // ── 7. Students ───────────────────────────────────────────────────────
        // Use typed Faker<T> — avoids the non-generic Faker.UseSeed() issue
        // and the Random ambiguity with EF's DbFunctionsExtensions.Random
        var personFaker = new Faker<StudentProfile>("en")
            .UseSeed(SeedValue); // Typed Faker<T> has UseSeed — this is the correct API

        // We still need raw name/phone/address generation so use the
        // underlying DataSets via a typed faker's internal context.
        // Simplest fix: use a System.Random with the fixed seed instead of Bogus
        // for the fields that caused the ambiguity.
        var rng = new System.Random(SeedValue);

        string[] firstNames = ["Kamille", "Marcel", "Anika", "Lyda"];
        string[] lastNames = ["Baumbach", "Connelly", "O'Keefe", "Conroy"];
        string[] phones = ["+353 15 127 5172", "+353 71 965 1035",
                                "+353 21 345 6789", "+353 91 234 5678"];
        string[] addresses = [
            "5085 Geoffrey Forks, East Astrid, Pitcairn Islands",
            "31007 Schneider Shoals, Betteberg, Samoa",
            "12 Maple Drive, Blackrock, Dublin",
            "78 River Lane, Salthill, Galway"
        ];

        var studentEmails = new[] { "student1@vgc.ie", "student2@vgc.ie",
                                    "student3@vgc.ie", "student4@vgc.ie" };
        var studentProfiles = new List<StudentProfile>();
        int sNum = 1001;

        for (int i = 0; i < studentEmails.Length; i++)
        {
            var email = studentEmails[i];
            var fullName = $"{firstNames[i]} {lastNames[i]}";
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = fullName,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Student1234!");
            await userManager.AddToRoleAsync(user, "Student");

            // DOB: between 18 and 28 years ago
            var daysAgo = rng.Next(365 * 18, 365 * 28);
            studentProfiles.Add(new StudentProfile
            {
                IdentityUserId = user.Id,
                Name = fullName,
                Email = email,
                Phone = phones[i],
                Address = addresses[i],
                DateOfBirth = DateTime.Today.AddDays(-daysAgo),
                StudentNumber = $"VGC{sNum++}"
            });
        }
        db.StudentProfiles.AddRange(studentProfiles);
        await db.SaveChangesAsync();

        // ── 8. Enrolments ─────────────────────────────────────────────────────
        var enrolments = new List<CourseEnrolment>
        {
            new() { StudentProfileId = studentProfiles[0].Id, CourseId = courses[0].Id, EnrolDate = new DateTime(2025, 9, 1), Status = "Active" },
            new() { StudentProfileId = studentProfiles[1].Id, CourseId = courses[0].Id, EnrolDate = new DateTime(2025, 9, 1), Status = "Active" },
            new() { StudentProfileId = studentProfiles[2].Id, CourseId = courses[1].Id, EnrolDate = new DateTime(2025, 9, 1), Status = "Active" },
            new() { StudentProfileId = studentProfiles[3].Id, CourseId = courses[1].Id, EnrolDate = new DateTime(2025, 9, 1), Status = "Active" },
            new() { StudentProfileId = studentProfiles[0].Id, CourseId = courses[2].Id, EnrolDate = new DateTime(2025, 9, 5), Status = "Active" }
        };
        db.CourseEnrolments.AddRange(enrolments);
        await db.SaveChangesAsync();

        // ── 9. Attendance (weeks 1-4) ─────────────────────────────────────────
        // 80% present rate using System.Random with fixed seed – no Bogus ambiguity
        var attRng = new System.Random(SeedValue);
        var attendance = enrolments.SelectMany(e =>
            Enumerable.Range(1, 4).Select(week => new AttendanceRecord
            {
                CourseEnrolmentId = e.Id,
                WeekNumber = week,
                SessionDate = new DateTime(2025, 9, 1).AddDays((week - 1) * 7),
                Present = attRng.NextDouble() < 0.80
            })
        ).ToList();
        db.AttendanceRecords.AddRange(attendance);
        await db.SaveChangesAsync();

        // ── 10. Assignments ───────────────────────────────────────────────────
        var assignments = new List<Assignment>
        {
            new() { CourseId = courses[0].Id, Title = "Lab 1: Version Control with Git",    MaxScore = 100, DueDate = new DateTime(2025, 10, 15) },
            new() { CourseId = courses[0].Id, Title = "Lab 2: MVC Web Application",         MaxScore = 100, DueDate = new DateTime(2025, 11, 20) },
            new() { CourseId = courses[0].Id, Title = "Project: Final Portfolio",            MaxScore = 200, DueDate = new DateTime(2026,  1, 30) },
            new() { CourseId = courses[1].Id, Title = "Assignment 1: Data Cleaning",        MaxScore = 50,  DueDate = new DateTime(2025, 10, 20) },
            new() { CourseId = courses[1].Id, Title = "Assignment 2: Visualisation Report", MaxScore = 50,  DueDate = new DateTime(2025, 12,  5) },
        };
        db.Assignments.AddRange(assignments);
        await db.SaveChangesAsync();

        // ── 11. Assignment results (System.Random, rounded to 2dp) ────────────
        // Using System.Random avoids the EF DbFunctionsExtensions.Random clash
        var scoreRng = new System.Random(SeedValue);
        decimal RndScore(double min, double max) =>
            Math.Round((decimal)(min + scoreRng.NextDouble() * (max - min)), 2);

        var assignmentResults = new List<AssignmentResult>
        {
            new() { AssignmentId = assignments[0].Id, StudentProfileId = studentProfiles[0].Id, Score = RndScore(55, 98), Feedback = "Good use of branching strategy." },
            new() { AssignmentId = assignments[0].Id, StudentProfileId = studentProfiles[1].Id, Score = RndScore(40, 85), Feedback = "Commit messages need improvement." },
            new() { AssignmentId = assignments[1].Id, StudentProfileId = studentProfiles[0].Id, Score = RndScore(60, 98), Feedback = "Excellent MVC separation." },
            new() { AssignmentId = assignments[1].Id, StudentProfileId = studentProfiles[1].Id, Score = RndScore(45, 80), Feedback = "Views need validation." },
            new() { AssignmentId = assignments[3].Id, StudentProfileId = studentProfiles[2].Id, Score = RndScore(30, 50), Feedback = "Some null handling missed." },
            new() { AssignmentId = assignments[3].Id, StudentProfileId = studentProfiles[3].Id, Score = RndScore(35, 50), Feedback = "Well-structured pipeline." }
        };
        db.AssignmentResults.AddRange(assignmentResults);
        await db.SaveChangesAsync();

        // ── 12. Exams ─────────────────────────────────────────────────────────
        var exams = new List<Exam>
        {
            new() { CourseId = courses[0].Id, Title = "Semester 1 Exam",  Date = new DateTime(2026, 1, 15), MaxScore = 100, ResultsReleased = true  },
            new() { CourseId = courses[0].Id, Title = "Semester 2 Exam",  Date = new DateTime(2026, 5, 10), MaxScore = 100, ResultsReleased = false },
            new() { CourseId = courses[1].Id, Title = "Final Examination", Date = new DateTime(2026, 1, 18), MaxScore = 100, ResultsReleased = true  }
        };
        db.Exams.AddRange(exams);
        await db.SaveChangesAsync();

        // ── 13. Exam results (System.Random, rounded to 2dp) ─────────────────
        var examRng = new System.Random(SeedValue + 1);
        decimal RndExam(double min, double max) =>
            Math.Round((decimal)(min + examRng.NextDouble() * (max - min)), 2);

        decimal s1 = RndExam(50, 98), s2 = RndExam(40, 85);
        decimal s3 = RndExam(55, 95), s4 = RndExam(38, 80);

        db.ExamResults.AddRange(
            new ExamResult { ExamId = exams[0].Id, StudentProfileId = studentProfiles[0].Id, Score = s1, Grade = ExamResult.CalculateGrade(s1, 100) },
            new ExamResult { ExamId = exams[0].Id, StudentProfileId = studentProfiles[1].Id, Score = s2, Grade = ExamResult.CalculateGrade(s2, 100) },
            new ExamResult { ExamId = exams[1].Id, StudentProfileId = studentProfiles[0].Id, Score = s1 + 5, Grade = ExamResult.CalculateGrade(s1 + 5, 100) },
            new ExamResult { ExamId = exams[1].Id, StudentProfileId = studentProfiles[1].Id, Score = s2 + 3, Grade = ExamResult.CalculateGrade(s2 + 3, 100) },
            new ExamResult { ExamId = exams[2].Id, StudentProfileId = studentProfiles[2].Id, Score = s3, Grade = ExamResult.CalculateGrade(s3, 100) },
            new ExamResult { ExamId = exams[2].Id, StudentProfileId = studentProfiles[3].Id, Score = s4, Grade = ExamResult.CalculateGrade(s4, 100) }
        );
        await db.SaveChangesAsync();
    }
}
