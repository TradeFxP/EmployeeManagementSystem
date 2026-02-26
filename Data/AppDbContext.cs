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
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<Epic> Epics { get; set; }
        public DbSet<Feature> Features { get; set; }
        public DbSet<Story> Stories { get; set; }

        public DbSet<Team> Teams { get; set; }

        // Custom Fields for Tasks
        public DbSet<TaskCustomField> TaskCustomFields { get; set; }
        public DbSet<TaskFieldValue> TaskFieldValues { get; set; }
        
        // Task History
        public DbSet<TaskHistory> TaskHistories { get; set; }

        // Board Permissions
        public DbSet<BoardPermission> BoardPermissions { get; set; }

        // Email Logs
        public DbSet<EmailLog> EmailLogs { get; set; }

        // Excel Import Logs
        public DbSet<ExcelImportLog> ExcelImportLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<DailyReport>()
                .HasIndex(r => new { r.ApplicationUserId, r.Date })
                .IsUnique();


            builder.Entity<DailyReport>()
                .Property(r => r.Date)
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

            builder.Entity<AssignedTask>()
                .HasOne(t => t.AssignedBy)
                .WithMany()
                .HasForeignKey(t => t.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AssignedTask>()
                .HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskItem>()
                .HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TaskItem>()
                .HasOne(t => t.AssignedByUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskItem>()
                .HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaskItem>()
                .HasOne(t => t.PreviousColumn)
                .WithMany()
                .HasForeignKey(t => t.PreviousColumnId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for archived tasks lookup
            builder.Entity<TaskItem>()
                .HasIndex(t => new { t.TeamName, t.IsArchived });

            // BoardPermission configuration
            builder.Entity<BoardPermission>()
                .HasIndex(p => new { p.UserId, p.TeamName })
                .IsUnique();

            // Feature Assignment
            builder.Entity<Feature>()
                .HasOne(f => f.AssignedToUser)
                .WithMany()
                .HasForeignKey(f => f.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Epic Assignment
            builder.Entity<Epic>()
                .HasOne(e => e.AssignedToUser)
                .WithMany()
                .HasForeignKey(e => e.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Story Assignment
            builder.Entity<Story>()
                .HasOne(s => s.AssignedToUser)
                .WithMany()
                .HasForeignKey(s => s.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Project Membership
            builder.Entity<ProjectMember>()
                .HasOne(pm => pm.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectMember>()
                .HasOne(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
