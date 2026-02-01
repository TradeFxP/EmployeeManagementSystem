using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UserRoles.Models;

namespace UserRoles.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<DailyReport> DailyReports => Set<DailyReport>();
        public DbSet<AssignedTask> AssignedTasks { get; set; }

        public DbSet<TaskItem> TaskItems { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<DailyReport>()
                .HasIndex(r => new { r.ApplicationUserId, r.Date })
                .IsUnique();

            builder.Entity<DailyReport>()
                .Property(r => r.Date)
                .HasColumnType("date");
        }
    }
}
