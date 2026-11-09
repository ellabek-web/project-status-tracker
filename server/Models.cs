using System.ComponentModel.DataAnnotations;

namespace ProjectStatusTracker.Server;

public enum Role { President, EVP, VP, Director, Manager, ScrumMaster, Developer }

public enum SubtaskStatus { NotStarted, InProgress, Done, Blocked }

public enum Cadence { Weekly, Fortnightly }

public enum ReportStatus { Draft, Reviewed }

public class User
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(200)] public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
    public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
}

public class Project
{
    public int Id { get; set; }
    [MaxLength(300)] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int ManagerId { get; set; }
    public User? Manager { get; set; }
    public DateOnly TargetDate { get; set; }
    public Cadence Cadence { get; set; } = Cadence.Weekly;
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public class TaskItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateOnly DueDate { get; set; }
    public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
}

public class Subtask
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public TaskItem? Task { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateOnly DueDate { get; set; }
    public SubtaskStatus Status { get; set; } = SubtaskStatus.NotStarted;
    public string BlockerNote { get; set; } = "";
}

public class Report
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public Cadence Cadence { get; set; } = Cadence.Weekly;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Summary { get; set; } = "";
    public string CompletedText { get; set; } = "";
    public string InProgressText { get; set; } = "";
    public string BlockedText { get; set; } = "";
    public string RisksText { get; set; } = "";
    public string NextStepsText { get; set; } = "";
    public string SlidesJson { get; set; } = "[]";
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- DTOs ----
public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, int UserId, string Name, string Email, string Role);
public record CreateProjectRequest(string Name, string Description, int ManagerId, DateOnly TargetDate, Cadence Cadence);
public record CreateTaskRequest(string Title, string Description, int OwnerId, DateOnly DueDate);
public record UpdateTaskRequest(string? Title, string? Description, int? OwnerId, DateOnly? DueDate);
public record CreateSubtaskRequest(string Title, int OwnerId, DateOnly DueDate, SubtaskStatus Status, string? BlockerNote);
public record UpdateSubtaskRequest(SubtaskStatus? Status, string? BlockerNote, string? Title, int? OwnerId, DateOnly? DueDate);
public record GenerateReportRequest(int? ProjectId, Cadence Cadence, DateOnly PeriodEnd);
public record ProjectDto(int Id, string Name, string Description, int ManagerId, string ManagerName, DateOnly TargetDate, string Cadence, int TotalSubtasks, int DoneSubtasks, int ProgressPercent, string Health);
public record TaskDto(int Id, int ProjectId, string Title, string Description, int OwnerId, string OwnerName, DateOnly DueDate, int TotalSubtasks, int DoneSubtasks, int ProgressPercent);
public record SubtaskDto(int Id, int TaskId, string Title, int OwnerId, string OwnerName, DateOnly DueDate, string Status, string BlockerNote);
public record ReportDto(int Id, int? ProjectId, string Title, string Cadence, DateOnly PeriodStart, DateOnly PeriodEnd, string Summary, string CompletedText, string InProgressText, string BlockedText, string RisksText, string NextStepsText, string Status, DateTime CreatedAt, string SlidesJson);
public record SlideDto(string Title, string[] Bullets);
