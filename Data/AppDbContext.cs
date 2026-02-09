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

       
        public DbSet<TeamColumn> TeamColumns { get; set; }

        public DbSet<UserTeam> UserTeams { get; set; }

        // Project Management Hierarchy
        public DbSet<Project> Projects { get; set; }
        public DbSet<Epic> Epics { get; set; }
        public DbSet<Feature> Features { get; set; }
        public DbSet<Story> Stories { get; set; }

        public DbSet<Team> Teams { get; set; }

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
