using MedicalImageAI.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedicalImageAI.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet representing the ImageAnalysisJob entity, which is mapped to a table in the database.
    /// By convention, the table name is inferred from the DbSet property name unless configured otherwise.
    /// </summary>
    public DbSet<ImageAnalysisJob> ImageAnalysisJobs { get; set; }

    // --- We can override OnModelCreating here for more advanced configuration if needed later ---
    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     base.OnModelCreating(modelBuilder);
    //     // Example: modelBuilder.Entity<ImageAnalysisJob>().ToTable("ImageJobs");
    // }
}
