using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectStatusTracker.Server.Data;
using ProjectStatusTracker.Server.Services;

namespace ProjectStatusTracker.Server.Controllers;

[ApiController, Authorize]
public class WorkController : ControllerBase
{
    private readonly AppDbContext _db;
    public WorkController(AppDbContext db) => _db = db;

    private Role Role => Authz.UserRole(User);
    private int Uid => Authz.UserId(User);

    private async Task<Project?> ProjectOfTask(int taskId) =>
        await _db.Tasks.Include(t => t.Project).Where(t => t.Id == taskId).Select(t => t.Project).FirstOrDefaultAsync();

    // Manager may edit only their own projects; ScrumMaster may edit any project.
    private bool CanManageProject(Project p) => Role == Role.ScrumMaster || (Role == Role.Manager && p.ManagerId == Uid);

    [HttpGet("api/projects/{projectId:int}/tasks")]
    public async Task<IActionResult> ListTasks(int projectId)
    {
        var tasks = await _db.Tasks.Include(t => t.Owner).Include(t => t.Subtasks)
            .Where(t => t.ProjectId == projectId).ToListAsync();
        return Ok(tasks.Select(t => new TaskDto(t.Id, t.ProjectId, t.Title, t.Description, t.OwnerId, t.Owner?.Name ?? "",
            t.DueDate, t.Subtasks.Count, t.Subtasks.Count(s => s.Status == SubtaskStatus.Done),
            Progress.Percent(t.Subtasks.Count(s => s.Status == SubtaskStatus.Done), t.Subtasks.Count))));
    }

    [HttpPost("api/projects/{projectId:int}/tasks")]
    public async Task<IActionResult> CreateTask(int projectId, [FromBody] CreateTaskRequest req)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) return NotFound();
        if (!CanManageProject(project)) return Forbid();
        var t = new TaskItem { ProjectId = projectId, Title = req.Title, Description = req.Description, OwnerId = req.OwnerId, DueDate = req.DueDate };
        _db.Tasks.Add(t);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ListTasks), new { projectId }, new { t.Id, t.Title });
    }

    [HttpPut("api/tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest req)
    {
        var t = await _db.Tasks.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == id);
        if (t == null) return NotFound();
        if (!CanManageProject(t.Project!)) return Forbid();
        if (req.Title != null) t.Title = req.Title;
        if (req.Description != null) t.Description = req.Description;
        if (req.OwnerId != null) t.OwnerId = req.OwnerId.Value;
        if (req.DueDate != null) t.DueDate = req.DueDate.Value;
        await _db.SaveChangesAsync();
        return Ok(new { t.Id, t.Title });
    }

    [HttpGet("api/tasks/{taskId:int}/subtasks")]
    public async Task<IActionResult> ListSubtasks(int taskId)
    {
        var subs = await _db.Subtasks.Include(s => s.Owner).Where(s => s.TaskId == taskId).OrderBy(s => s.DueDate).ToListAsync();
        return Ok(subs.Select(s => new SubtaskDto(s.Id, s.TaskId, s.Title, s.OwnerId, s.Owner?.Name ?? "", s.DueDate, s.Status.ToString(), s.BlockerNote)));
    }

    [HttpPost("api/tasks/{taskId:int}/subtasks")]
    public async Task<IActionResult> CreateSubtask(int taskId, [FromBody] CreateSubtaskRequest req)
    {
        var project = await ProjectOfTask(taskId);
        if (project == null) return NotFound();
        if (!CanManageProject(project)) return Forbid(); // Developers cannot create subtasks
        var s = new Subtask { TaskId = taskId, Title = req.Title, OwnerId = req.OwnerId, DueDate = req.DueDate, Status = req.Status, BlockerNote = req.BlockerNote ?? "" };
        _db.Subtasks.Add(s);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ListSubtasks), new { taskId }, new { s.Id, s.Title });
    }

    [HttpPut("api/subtasks/{id:int}")]
    public async Task<IActionResult> UpdateSubtask(int id, [FromBody] UpdateSubtaskRequest req)
    {
        var s = await _db.Subtasks.Include(x => x.Task).ThenInclude(t => t.Project).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var project = s.Task!.Project!;
        if (Role == Role.Developer)
        {
            // Developers update only their own subtasks, status only.
            if (s.OwnerId != Uid) return Forbid();
            if (req.Status != null) s.Status = req.Status.Value;
        }
        else if (Role == Role.ScrumMaster)
        {
            if (req.Status != null) s.Status = req.Status.Value;
            if (req.BlockerNote != null) s.BlockerNote = req.BlockerNote;
            if (req.Title != null) s.Title = req.Title;
        }
        else if (Role == Role.Manager)
        {
            if (!CanManageProject(project)) return Forbid();
            if (req.Status != null) s.Status = req.Status.Value;
            if (req.BlockerNote != null) s.BlockerNote = req.BlockerNote;
            if (req.Title != null) s.Title = req.Title;
            if (req.OwnerId != null) s.OwnerId = req.OwnerId.Value;
            if (req.DueDate != null) s.DueDate = req.DueDate.Value;
        }
        else return Forbid(); // President, EVP, VP, Director: view-only
        await _db.SaveChangesAsync();
        return Ok(new SubtaskDto(s.Id, s.TaskId, s.Title, s.OwnerId, "", s.DueDate, s.Status.ToString(), s.BlockerNote));
    }
}
