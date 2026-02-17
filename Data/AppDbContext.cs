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

        // Custom Fields for Tasks
        public DbSet<TaskCustomField> TaskCustomFields { get; set; }
        public DbSet<TaskFieldValue> TaskFieldValues { get; set; }
        
        // Task History
        public DbSet<TaskHistory> TaskHistories { get; set; }

        // Email Logs
        public DbSet<EmailLog> EmailLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<DailyReport>()
                .HasIndex(r => new { r.ApplicationUserId, r.Date })
                .IsUnique();

            builder.Entity<DailyReport>()
                .Property(r => r.Date)
                .HasColumnType("date");
            // ✅ Configure Users date fields
            builder.Entity<Users>()
                .Property(u => u.DateOfBirth)
                .HasColumnType("date");


            builder.Entity<Users>()
                .Property(u => u.DateOfJoining)
                .HasColumnType("date");

            // Configure TaskFieldValue relationships with cascade delete
            builder.Entity<TaskFieldValue>()
                .HasOne(v => v.Field)
                .WithMany(f => f.FieldValues)
                .HasForeignKey(v => v.FieldId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskFieldValue>()
                .HasOne(v => v.Task)
                .WithMany(t => t.CustomFieldValues)
                .HasForeignKey(v => v.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure TaskHistory relationships
            builder.Entity<TaskHistory>()
                .HasOne(h => h.Task)
                .WithMany(t => t.History)
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskHistory>()
                .HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for performance
            builder.Entity<TaskHistory>()
                .HasIndex(h => new { h.TaskId, h.ChangedAt });

            // EmailLog configuration
            builder.Entity<EmailLog>()
                .HasOne(e => e.SentByUser)
                .WithMany()
                .HasForeignKey(e => e.SentByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<EmailLog>()
                .HasIndex(e => new { e.ToEmail, e.SentAt });

            // Review workflow FK configs
            builder.Entity<TaskItem>()
                .HasOne(t => t.ReviewedByUser)
                .WithMany()
                .HasForeignKey(t => t.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskItem>()
                .HasOne(t => t.CompletedByUser)
                .WithMany()
                .HasForeignKey(t => t.CompletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskItem>()
                .HasOne(t => t.PreviousColumn)
                .WithMany()
                .HasForeignKey(t => t.PreviousColumnId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for archived tasks lookup
            builder.Entity<TaskItem>()
                .HasIndex(t => new { t.TeamName, t.IsArchived });

        }
    }
}
