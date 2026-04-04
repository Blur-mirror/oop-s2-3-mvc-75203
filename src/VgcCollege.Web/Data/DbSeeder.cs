using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Data;

/// Runs once at startup (inside EnsureSeeded) to populate an empty database
/// with roles, demo accounts, and realistic Bogus-generated content.
///
/// Seeded credentials (also in README):
///   admin@vgc.ie        / Admin1234!
///   faculty1@vgc.ie     / Faculty1234!
///   faculty2@vgc.ie     / Faculty1234!
///   student1@vgc.ie     / Student1234!
///   student2@vgc.ie     / Student1234!
///   student3@vgc.ie     / Student1234!
///   student4@vgc.ie     / Student1234!

public static class DbSeeder
{
    //data is deterministic across runs
    private const int BoguseSeed = 42;

    public static async Task EnsureSeeded(IServiceProvider services)
    {
        var db          = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply any pending migrations automatically
        await db.Database.MigrateAsync();

        // Guard: skip if already seeded
        if (await db.Branches.AnyAsync()) return;

        //Roles
        string[] roles = ["Administrator", "Faculty", "Student"];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        //Admin user
        var admin = new ApplicationUser
        {
            UserName    = "admin@vgc.ie",
            Email       = "admin@vgc.ie",
            DisplayName = "System Administrator",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "Admin1234!");
        await userManager.AddToRoleAsync(admin, "Administrator");

        //Branches
        var branchFaker = new Faker<Branch>("en")
            .RuleFor(b => b.Name, f => $"VGC {f.Address.City()} Campus")
            .RuleFor(b => b.Address, f => f.Address.FullAddress())
            .UseSeed(BoguseSeed);

        var branches = new List<Branch>
        {
            new() { Name = "VGC Dublin City Campus",    Address = "12 O'Connell Street, Dublin 1" },
            new() { Name = "VGC Cork South Campus",     Address = "45 Patrick Street, Cork" },
            new() { Name = "VGC Galway West Campus",    Address = "78 Shop Street, Galway" }
        };
        db.Branches.AddRange(branches);
        await db.SaveChangesAsync();

        //Courses
        var courseNames = new[]
        {
            ("Software Development", 0),
            ("Data Science & Analytics", 0),
            ("Cybersecurity Fundamentals", 1),
            ("Network Engineering", 1),
            ("Digital Marketing", 2),
            ("Business Computing", 2)
        };

        var courses = courseNames.Select((cn, i) => new Course
        {
            Name      = cn.Item1,
            BranchId  = branches[cn.Item2].Id,
            StartDate = new DateTime(2025, 9, 1),
            EndDate   = new DateTime(2026, 5, 31)
        }).ToList();

        db.Courses.AddRange(courses);
        await db.SaveChangesAsync();

        //Faculty users + profiles
        var facultyData = new[]
        {
            ("faculty1@vgc.ie", "Dr. Sarah O'Brien",   "+353 1 234 5678"),
            ("faculty2@vgc.ie", "Prof. James Murphy",  "+353 21 456 7890")
        };

        var facultyProfiles = new List<FacultyProfile>();
        foreach (var (email, name, phone) in facultyData)
        {
            var user = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                DisplayName    = name,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Faculty1234!");
            await userManager.AddToRoleAsync(user, "Faculty");

            var profile = new FacultyProfile
            {
                IdentityUserId = user.Id,
                Name           = name,
                Email          = email,
                Phone          = phone
            };
            facultyProfiles.Add(profile);
        }
        db.FacultyProfiles.AddRange(facultyProfiles);
        await db.SaveChangesAsync();

        //Faculty course assignments
        // faculty1 to courses 0,1,2 (Tutor on course 0, Lecturer elsewhere)
        // faculty2 to courses 3,4,5
        var assignments = new List<FacultyCourseAssignment>
        {
            new() { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[0].Id, Role = "Tutor" },
            new() { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[1].Id, Role = "Lecturer" },
            new() { FacultyProfileId = facultyProfiles[0].Id, CourseId = courses[2].Id, Role = "Lecturer" },
            new() { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[3].Id, Role = "Tutor" },
            new() { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[4].Id, Role = "Lecturer" },
            new() { FacultyProfileId = facultyProfiles[1].Id, CourseId = courses[5].Id, Role = "Lecturer" }
        };
        db.FacultyCourseAssignments.AddRange(assignments);
        await db.SaveChangesAsync();

        //Student users + profiles
        var studentEmails = new[]
        {
            "student1@vgc.ie", "student2@vgc.ie", "student3@vgc.ie", "student4@vgc.ie"
        };

        var personFaker = new Faker("en").UseSeed(BoguseSeed);
        var studentProfiles = new List<StudentProfile>();
        int studentNum = 1001;

        foreach (var email in studentEmails)
        {
            var firstName = personFaker.Name.FirstName();
            var lastName  = personFaker.Name.LastName();
            var fullName  = $"{firstName} {lastName}";

            var user = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                DisplayName    = fullName,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Student1234!");
            await userManager.AddToRoleAsync(user, "Student");

            var profile = new StudentProfile
            {
                IdentityUserId = user.Id,
                Name           = fullName,
                Email          = email,
                Phone          = personFaker.Phone.PhoneNumber("+353 ## ### ####"),
                Address        = personFaker.Address.FullAddress(),
                DateOfBirth    = personFaker.Date.Past(25, DateTime.Today.AddYears(-18)),
                StudentNumber  = $"VGC{studentNum++}"
            };
            studentProfiles.Add(profile);
        }
        db.StudentProfiles.AddRange(studentProfiles);
        await db.SaveChangesAsync();

        //Enrolments
        // Students 0,1 to Software Development (course 0)
        // Students 2,3 to Data Science (course 1)
        // Student 0    to also Cybersecurity (course 2) for multi-enrolment demo
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

        //Attendance records (weeks 1-4)
        var attendanceFaker = new Faker().UseSeed(BoguseSeed);
        var attendanceRecords = new List<AttendanceRecord>();

        foreach (var enrolment in enrolments)
        {
            for (int week = 1; week <= 4; week++)
            {
                attendanceRecords.Add(new AttendanceRecord
                {
                    CourseEnrolmentId = enrolment.Id,
                    WeekNumber        = week,
                    SessionDate       = new DateTime(2025, 9, 1).AddDays((week - 1) * 7),
                    Present           = attendanceFaker.Random.Bool(0.8f) // 80% attendance rate
                });
            }
        }
        db.AttendanceRecords.AddRange(attendanceRecords);
        await db.SaveChangesAsync();

        //Assignments
        var assignments2 = new List<Assignment>
        {
            // Software Development course
            new() { CourseId = courses[0].Id, Title = "Lab 1: Version Control with Git",    MaxScore = 100, DueDate = new DateTime(2025, 10, 15) },
            new() { CourseId = courses[0].Id, Title = "Lab 2: MVC Web Application",         MaxScore = 100, DueDate = new DateTime(2025, 11, 20) },
            new() { CourseId = courses[0].Id, Title = "Project: Final Portfolio",            MaxScore = 200, DueDate = new DateTime(2026,  1, 30) },
            // Data Science course
            new() { CourseId = courses[1].Id, Title = "Assignment 1: Data Cleaning",        MaxScore = 50,  DueDate = new DateTime(2025, 10, 20) },
            new() { CourseId = courses[1].Id, Title = "Assignment 2: Visualisation Report", MaxScore = 50,  DueDate = new DateTime(2025, 12,  5) },
        };
        db.Assignments.AddRange(assignments2);
        await db.SaveChangesAsync();

        //Assignment results
        var scoreFaker = new Faker().UseSeed(BoguseSeed);

        // Software Development students get results for lab 1 & 2
        var assignmentResults = new List<AssignmentResult>
        {
            new() { AssignmentId = assignments2[0].Id, StudentProfileId = studentProfiles[0].Id, Score =  scoreFaker.Random.Decimal(55, 98), Feedback = "Good use of branching strategy." },
            new() { AssignmentId = assignments2[0].Id, StudentProfileId = studentProfiles[1].Id, Score =  scoreFaker.Random.Decimal(40, 85), Feedback = "Commit messages need improvement." },
            new() { AssignmentId = assignments2[1].Id, StudentProfileId = studentProfiles[0].Id, Score =  scoreFaker.Random.Decimal(60, 98), Feedback = "Excellent MVC separation." },
            new() { AssignmentId = assignments2[1].Id, StudentProfileId = studentProfiles[1].Id, Score =  scoreFaker.Random.Decimal(45, 80), Feedback = "Views need validation." },
            // Data Science students
            new() { AssignmentId = assignments2[3].Id, StudentProfileId = studentProfiles[2].Id, Score =  scoreFaker.Random.Decimal(30, 50), Feedback = "Some null handling missed." },
            new() { AssignmentId = assignments2[3].Id, StudentProfileId = studentProfiles[3].Id, Score =  scoreFaker.Random.Decimal(35, 50), Feedback = "Well-structured pipeline." }
        };
        db.AssignmentResults.AddRange(assignmentResults);
        await db.SaveChangesAsync();

        //Exams
        var exams = new List<Exam>
        {
            new() { CourseId = courses[0].Id, Title = "Semester 1 Exam",  Date = new DateTime(2026, 1, 15), MaxScore = 100, ResultsReleased = true  },
            new() { CourseId = courses[0].Id, Title = "Semester 2 Exam",  Date = new DateTime(2026, 5, 10), MaxScore = 100, ResultsReleased = false }, // provisional
            new() { CourseId = courses[1].Id, Title = "Final Examination", Date = new DateTime(2026, 1, 18), MaxScore = 100, ResultsReleased = true  }
        };
        db.Exams.AddRange(exams);
        await db.SaveChangesAsync();

        //Exam results
        var examScoreFaker = new Faker().UseSeed(BoguseSeed + 1);

        decimal s1Score = examScoreFaker.Random.Decimal(50, 98);
        decimal s2Score = examScoreFaker.Random.Decimal(40, 85);
        decimal s3Score = examScoreFaker.Random.Decimal(55, 95);
        decimal s4Score = examScoreFaker.Random.Decimal(38, 80);

        var examResults = new List<ExamResult>
        {
            // Semester 1 – released
            new() { ExamId = exams[0].Id, StudentProfileId = studentProfiles[0].Id, Score = s1Score, Grade = ExamResult.CalculateGrade(s1Score, 100) },
            new() { ExamId = exams[0].Id, StudentProfileId = studentProfiles[1].Id, Score = s2Score, Grade = ExamResult.CalculateGrade(s2Score, 100) },
            // Semester 2 – provisional (ResultsReleased = false)
            new() { ExamId = exams[1].Id, StudentProfileId = studentProfiles[0].Id, Score = s1Score + 5, Grade = ExamResult.CalculateGrade(s1Score + 5, 100) },
            new() { ExamId = exams[1].Id, StudentProfileId = studentProfiles[1].Id, Score = s2Score + 3, Grade = ExamResult.CalculateGrade(s2Score + 3, 100) },
            // Data Science final
            new() { ExamId = exams[2].Id, StudentProfileId = studentProfiles[2].Id, Score = s3Score, Grade = ExamResult.CalculateGrade(s3Score, 100) },
            new() { ExamId = exams[2].Id, StudentProfileId = studentProfiles[3].Id, Score = s4Score, Grade = ExamResult.CalculateGrade(s4Score, 100) }
        };
        db.ExamResults.AddRange(examResults);
        await db.SaveChangesAsync();
    }
}
