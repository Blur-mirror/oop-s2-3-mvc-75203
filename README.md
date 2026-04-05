# VGC College  Student Management System

ASP.NET Core 8 MVC application for managing college students, faculty, courses, enrolments, attendance, and academic results.

---

## Seeded Demo Accounts

| Role          | Email                | Password       |
|---------------|----------------------|----------------|
| Administrator | <admin@vgc.ie>         | Admin1234!     |
| Faculty       | <faculty1@vgc.ie>      | Faculty1234!   |
| Faculty       | <faculty2@vgc.ie>      | Faculty1234!   |
| Student       | <student1@vgc.ie>      | Student1234!   |
| Student       | <student2@vgc.ie>      | Student1234!   |
| Student       | <student3@vgc.ie>      | Student1234!   |
| Student       | <student4@vgc.ie>      | Student1234!   |

The seed data also includes 3 branches, 6 courses, 5 enrolments, 4 weeks of attendance records, 2 assignments per course, and both released and provisional exam results, so the app is immediately usable without manual data entry.

---

## How to Run Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

### Steps

```bash
# 1. Clone the repository
git clone https://github.com/Blur-mirror/oop-s2-3-mvc-75203.git
cd oop-s2-3-mvc-75203

# 2. Navigate to the web project
cd src/VgcCollege.Web

# 3. Run the app
#    On the first run, EF Core will create the SQLite database (vgccollege.db)
#    and seed all demo data automatically.
dotnet run
```

Then open your browser at `https://localhost:5001` (or the port shown in the terminal).

---

## How to Run Tests

```bash
# From the solution root
dotnet test
```

Tests use the **EF Core InMemory** provider, no database file required. They are deterministic and safe to run in parallel.

### What the tests cover

| # | Test | Category |
|---|------|----------|
| 1–4 | Grade letter calculation (A/B/C/D/F thresholds) | Grade Logic |
| 5 | Assignment result percentage calculation | Grade Logic |
| 6 | Exam result percentage calculation | Grade Logic |
| 7 | Duplicate enrolment detection | Enrolment Rules |
| 8 | Student can enrol in multiple courses | Enrolment Rules |
| 9 | Exam score hidden when `ResultsReleased = false` | Visibility Rules |
| 10 | Exam score visible when `ResultsReleased = true` | Visibility Rules |
| 11 | Faculty query filters, only own-course students visible | Auth Filtering |
| 12 | Tutor role check grants contact detail access | Auth Filtering |
| 13 | Attendance upsert updates existing week record | Attendance |
| 14 | Score exceeding MaxScore is flagged as invalid | Validation |
| 15 | Duplicate assignment result detection | Validation |

---

## Project Structure

```
oop-s2-3-mvc-75203/
├── src/
│   └── VgcCollege.Web/          # Main MVC application
│       ├── Controllers/
│       │   ├── AdminController.cs    # Admin CRUD (branches, courses, enrolments, exam release)
│       │   ├── FacultyController.cs  # Faculty gradebook, attendance, contact details
│       │   ├── StudentController.cs  # Student self-service, exam result gating
│       │   └── HomeController.cs     # Public landing + role redirect
│       ├── Data/
│       │   ├── ApplicationDbContext.cs  # EF Core context + all relationship config
│       │   └── DbSeeder.cs             # Bogus-powered seed data
│       ├── Models/               # Domain entities + ApplicationUser
│       └── Views/                # Razor views per controller
├── tests/
│   └── VgcCollege.Tests/        # xUnit test project
│       └── VgcCollegeTests.cs   # 15 tests (InMemory EF)
└── .github/
    └── workflows/
        └── ci.yml               # GitHub Actions: restore - build - test
```

---

## Design Decisions

### Authorization

- Every controller and action that requires a role uses `[Authorize(Roles = "...")]`.
- Controller-level attributes protect entire controller classes; no action is reachable without the correct role.
- Data access is **also filtered server-side** inside each query (e.g. `FacultyController` joins through `FacultyCourseAssignments` to ensure faculty only query their own students). Hiding links in the UI alone would not be sufficient.

### Exam Result Release

- `Exam.ResultsReleased` is a simple boolean set by Admins via the Exams page.
- `StudentController.ExamResults` maps results to a `StudentExamResultViewModel` where `Score` and `Grade` are nullable, they are set to `null` when `!exam.ResultsReleased`. This means even if someone inspects the raw HTTP response, the values are absent.

### Faculty Contact Access

- Faculty with the role `"Tutor"` in `FacultyCourseAssignment` may view student contact details.
- Faculty with `"Lecturer"` or `"Lab Assistant"` roles cannot.
- This is enforced by filtering `FacultyCourseAssignment.Role == "Tutor"` in the server-side query.

### Cascade Deletes

- Deleting a `CourseEnrolment` cascades to its `AttendanceRecord` rows.
- Deleting a `Course` cascades to `Assignment`, `Exam`, and their results.
- Deleting a `Branch` is **restricted** if it still has courses (explicit guard in AdminController).

### Seeding

- `DbSeeder.EnsureSeeded` is called at startup and is a no-op if any branches already exist.
- [Bogus](https://github.com/bchavez/Bogus) generates realistic names, phone numbers, and addresses with a fixed seed (`42`) so data is identical on every fresh run.

### Database

- SQLite via EF Core. The `.db` file is created automatically beside the app on first run.
- Migrations are included in `src/VgcCollege.Web/Migrations/`, run `dotnet ef database update` from the web project folder if you need to reset and re-apply manually.

---

## Running Migrations Manually (optional)

```bash
cd src/VgcCollege.Web
dotnet ef database drop --force
dotnet ef database update
dotnet run
```
