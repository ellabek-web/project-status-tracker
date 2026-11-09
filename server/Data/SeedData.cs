using Microsoft.EntityFrameworkCore;

namespace ProjectStatusTracker.Server.Data;

public static class SeedData
{
    public const string SeedPassword = "ChangeMe123!";

    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var users = new List<User>
        {
            new() { Name = "Ada President", Email = "president@demo.local", Role = Role.President },
            new() { Name = "Eva Executive", Email = "evp@demo.local", Role = Role.EVP },
            new() { Name = "Victor Vice", Email = "vp@demo.local", Role = Role.VP },
            new() { Name = "Daniel Okafor", Email = "director@demo.local", Role = Role.Director },
            new() { Name = "Maya Chen", Email = "manager@demo.local", Role = Role.Manager },
            new() { Name = "Sam Mwangi", Email = "scrum@demo.local", Role = Role.ScrumMaster },
            new() { Name = "Priya Nair", Email = "dev@demo.local", Role = Role.Developer },
            new() { Name = "Alex Rivera", Email = "dev2@demo.local", Role = Role.Developer },
        };
        foreach (var u in users) u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedPassword);
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        var byEmail = await db.Users.ToDictionaryAsync(u => u.Email);
        var maya = byEmail["manager@demo.local"];
        var priya = byEmail["dev@demo.local"];
        var alex = byEmail["dev2@demo.local"];
        var sam = byEmail["scrum@demo.local"];

        var project = new Project
        {
            Name = "Customer Portal Redesign",
            Description = "Redesign the customer portal to improve onboarding and self-service.",
            ManagerId = maya.Id,
            TargetDate = new DateOnly(2026, 12, 18),
            Cadence = Cadence.Weekly
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var tasks = new List<(TaskItem Task, List<(string Title, User Owner, DateOnly Due, SubtaskStatus Status, string Blocker)> Subs)>
        {
            (new TaskItem { ProjectId = project.Id, Title = "Design new onboarding flow", Description = "Research, wireframes and final UI for signup.", OwnerId = alex.Id, DueDate = new DateOnly(2026, 11, 20) },
                new() {
                    ("User research interviews", priya, new DateOnly(2026, 11, 3), SubtaskStatus.Done, ""),
                    ("Wireframes for signup", alex, new DateOnly(2026, 11, 6), SubtaskStatus.Done, ""),
                    ("Final UI mockups", alex, new DateOnly(2026, 11, 14), SubtaskStatus.InProgress, ""),
                    ("Design review with manager", maya, new DateOnly(2026, 11, 18), SubtaskStatus.NotStarted, ""),
                }),
            (new TaskItem { ProjectId = project.Id, Title = "Build login and account pages", Description = "Auth, login page and account settings.", OwnerId = priya.Id, DueDate = new DateOnly(2026, 11, 28) },
                new() {
                    ("Set up auth API", priya, new DateOnly(2026, 11, 4), SubtaskStatus.Done, ""),
                    ("Build login page", priya, new DateOnly(2026, 11, 14), SubtaskStatus.InProgress, ""),
                    ("Build account settings page", alex, new DateOnly(2026, 11, 21), SubtaskStatus.NotStarted, ""),
                    ("Fix password reset bug", alex, new DateOnly(2026, 11, 5), SubtaskStatus.Blocked, "Waiting for security review"),
                }),
            (new TaskItem { ProjectId = project.Id, Title = "Migrate customer data", Description = "Clean records and test the migration script.", OwnerId = sam.Id, DueDate = new DateOnly(2026, 12, 5) },
                new() {
                    ("Clean old customer records", sam, new DateOnly(2026, 11, 14), SubtaskStatus.InProgress, ""),
                    ("Test migration script", priya, new DateOnly(2026, 11, 28), SubtaskStatus.NotStarted, ""),
                }),
        };
        foreach (var (task, subs) in tasks)
        {
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
            foreach (var s in subs)
                db.Subtasks.Add(new Subtask { TaskId = task.Id, Title = s.Title, OwnerId = s.Owner.Id, DueDate = s.Due, Status = s.Status, BlockerNote = s.Blocker });
        }
        await db.SaveChangesAsync();
    }
}
