using Microsoft.EntityFrameworkCore;
using QuotationSystem.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Quotation> Quotations { get; set; }
    public DbSet<CourseType> CourseTypes { get; set; }
    public DbSet<CourseOption> CourseOptions { get; set; }
    public DbSet<QuotationCourse> QuotationCourses { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Enquiry> Enquiries { get; set; }
    public DbSet<QuotationCoursePrice> QuotationCoursePrices { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuotationCoursePrice>()
            .HasOne(qcp => qcp.QuotationCourse)
            .WithOne(qc => qc.QuotationCoursePrice)
            .HasForeignKey<QuotationCoursePrice>(qcp => qcp.QuotationCourseId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<QuotationCoursePrice>()
            .Property(qcp => qcp.FullCoursePrice)
            .HasPrecision(18, 2);
        modelBuilder.Entity<QuotationCoursePrice>()
            .Property(qcp => qcp.HalfCoursePrice)
            .HasPrecision(18, 2);
        modelBuilder.Entity<QuotationCourse>()
            .HasOne(qc => qc.Quotation)
            .WithMany(q => q.QuotationCourses)
            .HasForeignKey(qc => qc.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuotationCourse>()
            .HasOne(qc => qc.CourseOption)
            .WithMany()
            .HasForeignKey(qc => qc.CourseOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Quotation)
            .WithMany()
            .HasForeignKey(n => n.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Enquiry>()
            .HasOne(e => e.Quotation)
            .WithMany(q => q.Enquiries)
            .HasForeignKey(e => e.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseOption>()
            .HasOne(co => co.Course)
            .WithMany(c => c.CourseOptions)
            .HasForeignKey(co => co.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseOption>()
            .HasOne(co => co.CourseType)
            .WithMany(ct => ct.CourseOptions)
            .HasForeignKey(co => co.CourseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(true); // UserId is required, but User object is not
    }
}