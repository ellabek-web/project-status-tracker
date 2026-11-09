using Microsoft.EntityFrameworkCore;

namespace ProjectStatusTracker.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Project>()
            .HasOne(p => p.Manager).WithMany(u => u.ManagedProjects)
            .HasForeignKey(p => p.ManagerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Project).WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Owner).WithMany()
            .HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Subtask>()
            .HasOne(s => s.Task).WithMany(t => t.Subtasks)
            .HasForeignKey(s => s.TaskId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Subtask>()
            .HasOne(s => s.Owner).WithMany()
            .HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Project).WithMany()
            .HasForeignKey(r => r.ProjectId).OnDelete(DeleteBehavior.SetNull);
        base.OnModelCreating(modelBuilder);
    }
}
