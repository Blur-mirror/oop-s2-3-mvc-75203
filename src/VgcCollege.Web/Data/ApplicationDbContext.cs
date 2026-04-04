using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Data;

/// Central EF Core context for VGC College.
/// Inherits IdentityDbContext so Identity tables live in the same database.
/// All entity relationships and cascade rules are configured here.
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    //Domain DbSets
    public DbSet<Branch>                  Branches                 { get; set; }
    public DbSet<Course>                  Courses                  { get; set; }
    public DbSet<StudentProfile>          StudentProfiles          { get; set; }
    public DbSet<FacultyProfile>          FacultyProfiles          { get; set; }
    public DbSet<FacultyCourseAssignment> FacultyCourseAssignments { get; set; }
    public DbSet<CourseEnrolment>         CourseEnrolments         { get; set; }
    public DbSet<AttendanceRecord>        AttendanceRecords        { get; set; }
    public DbSet<Assignment>              Assignments              { get; set; }
    public DbSet<AssignmentResult>        AssignmentResults        { get; set; }
    public DbSet<Exam>                    Exams                    { get; set; }
    public DbSet<ExamResult>              ExamResults              { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Must call base for Identity tables

        //Branch to Course (one-to-many)
        builder.Entity<Course>()
            .HasOne(c => c.Branch)
            .WithMany(b => b.Courses)
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        //StudentProfile to ApplicationUser (one-to-one)
        builder.Entity<StudentProfile>()
            .HasOne(sp => sp.IdentityUser)
            .WithOne()
            .HasForeignKey<StudentProfile>(sp => sp.IdentityUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentProfile>()
            .HasIndex(sp => sp.StudentNumber)
            .IsUnique();

        //FacultyProfile to ApplicationUser (one-to-one)
        builder.Entity<FacultyProfile>()
            .HasOne(fp => fp.IdentityUser)
            .WithOne()
            .HasForeignKey<FacultyProfile>(fp => fp.IdentityUserId)
            .OnDelete(DeleteBehavior.Cascade);

        //FacultyCourseAssignment (many-to-many bridge)
        builder.Entity<FacultyCourseAssignment>()
            .HasOne(fca => fca.FacultyProfile)
            .WithMany(fp => fp.CourseAssignments)
            .HasForeignKey(fca => fca.FacultyProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FacultyCourseAssignment>()
            .HasOne(fca => fca.Course)
            .WithMany(c => c.FacultyAssignments)
            .HasForeignKey(fca => fca.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<FacultyCourseAssignment>()
            .HasIndex(fca => new { fca.FacultyProfileId, fca.CourseId })
            .IsUnique();

        //CourseEnrolment (Student to course)
        builder.Entity<CourseEnrolment>()
            .HasOne(ce => ce.StudentProfile)
            .WithMany(sp => sp.Enrolments)
            .HasForeignKey(ce => ce.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CourseEnrolment>()
            .HasOne(ce => ce.Course)
            .WithMany(c => c.Enrolments)
            .HasForeignKey(ce => ce.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CourseEnrolment>()
            .HasIndex(ce => new { ce.StudentProfileId, ce.CourseId })
            .IsUnique();

        //AttendanceRecord to CourseEnrolment
        builder.Entity<AttendanceRecord>()
            .HasOne(ar => ar.CourseEnrolment)
            .WithMany(ce => ce.AttendanceRecords)
            .HasForeignKey(ar => ar.CourseEnrolmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AttendanceRecord>()
            .HasIndex(ar => new { ar.CourseEnrolmentId, ar.WeekNumber })
            .IsUnique();

        //Assignment to Course
        builder.Entity<Assignment>()
            .HasOne(a => a.Course)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        //AssignmentResult
        builder.Entity<AssignmentResult>()
            .HasOne(ar => ar.Assignment)
            .WithMany(a => a.Results)
            .HasForeignKey(ar => ar.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AssignmentResult>()
            .HasOne(ar => ar.StudentProfile)
            .WithMany(sp => sp.AssignmentResults)
            .HasForeignKey(ar => ar.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AssignmentResult>()
            .HasIndex(ar => new { ar.AssignmentId, ar.StudentProfileId })
            .IsUnique();

        //Exam to Course
        builder.Entity<Exam>()
            .HasOne(e => e.Course)
            .WithMany(c => c.Exams)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        //ExamResult
        builder.Entity<ExamResult>()
            .HasOne(er => er.Exam)
            .WithMany(e => e.Results)
            .HasForeignKey(er => er.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamResult>()
            .HasOne(er => er.StudentProfile)
            .WithMany(sp => sp.ExamResults)
            .HasForeignKey(er => er.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ExamResult>()
            .HasIndex(er => new { er.ExamId, er.StudentProfileId })
            .IsUnique();

        // Decimal precision for all money/score columns
        builder.Entity<Assignment>().Property(a => a.MaxScore).HasColumnType("decimal(8,2)");
        builder.Entity<AssignmentResult>().Property(ar => ar.Score).HasColumnType("decimal(8,2)");
        builder.Entity<Exam>().Property(e => e.MaxScore).HasColumnType("decimal(8,2)");
        builder.Entity<ExamResult>().Property(er => er.Score).HasColumnType("decimal(8,2)");
    }
}
