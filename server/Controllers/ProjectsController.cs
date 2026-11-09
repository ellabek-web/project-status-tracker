using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectStatusTracker.Server.Data;
using ProjectStatusTracker.Server.Services;

namespace ProjectStatusTracker.Server.Controllers;

[ApiController, Authorize]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProjectsController(AppDbContext db) => _db = db;

    private static ProjectDto ToDto(Project p, DateOnly asOf)
    {
        var subs = p.Tasks.SelectMany(t => t.Subtasks).ToList();
        return new ProjectDto(p.Id, p.Name, p.Description, p.ManagerId, p.Manager?.Name ?? "", p.TargetDate,
            p.Cadence.ToString(), subs.Count, subs.Count(s => s.Status == SubtaskStatus.Done),
            Progress.Percent(subs.Count(s => s.Status == SubtaskStatus.Done), subs.Count), Progress.Health(subs, asOf));
    }

    [HttpGet("api/projects")]
    public async Task<IActionResult> List()
    {
        var projects = await _db.Projects.Include(p => p.Manager).Include(p => p.Tasks).ThenInclude(t => t.Subtasks).ToListAsync();
        return Ok(projects.Select(p => ToDto(p, DateOnly.FromDateTime(DateTime.UtcNow))));
    }

    [HttpGet("api/projects/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await _db.Projects.Include(x => x.Manager).Include(x => x.Tasks).ThenInclude(t => t.Subtasks).ThenInclude(s => s.Owner)
            .Include(x => x.Tasks).ThenInclude(t => t.Owner).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        return Ok(new
        {
            project = ToDto(p, DateOnly.FromDateTime(DateTime.UtcNow)),
            tasks = p.Tasks.Select(t => new TaskDto(t.Id, t.ProjectId, t.Title, t.Description, t.OwnerId, t.Owner?.Name ?? "",
                t.DueDate, t.Subtasks.Count, t.Subtasks.Count(s => s.Status == SubtaskStatus.Done),
                Progress.Percent(t.Subtasks.Count(s => s.Status == SubtaskStatus.Done), t.Subtasks.Count))),
            subtasks = p.Tasks.SelectMany(t => t.Subtasks).Select(s => new SubtaskDto(s.Id, s.TaskId, s.Title, s.OwnerId, s.Owner?.Name ?? "", s.DueDate, s.Status.ToString(), s.BlockerNote))
        });
    }

    [HttpPost("api/projects")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest req)
    {
        var role = Authz.UserRole(User);
        if (role != Role.Manager) return Forbid(); // only Managers own projects
        if (!await _db.Users.AnyAsync(u => u.Id == req.ManagerId)) return BadRequest(new { error = "Manager not found." });
        var p = new Project { Name = req.Name, Description = req.Description, ManagerId = req.ManagerId, TargetDate = req.TargetDate, Cadence = req.Cadence };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = p.Id }, new { p.Id, p.Name });
    }

    [HttpGet("api/users")]
    public async Task<IActionResult> Users() =>
        Ok(await _db.Users.Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString() }).ToListAsync());

    [HttpGet("api/dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var projects = await _db.Projects.Include(p => p.Manager).Include(p => p.Tasks).ThenInclude(t => t.Subtasks).ThenInclude(s => s.Owner).ToListAsync();
        var subs = projects.SelectMany(p => p.Tasks.SelectMany(t => t.Subtasks.Select(s => (Sub: s, Task: t, Project: p)))).ToList();
        var done = subs.Count(x => x.Sub.Status == SubtaskStatus.Done);
        return Ok(new
        {
            totalProjects = projects.Count,
            totalTasks = projects.Sum(p => p.Tasks.Count),
            totalSubtasks = subs.Count,
            doneSubtasks = done,
            progressPercent = Progress.Percent(done, subs.Count),
            projects = projects.Select(p => ToDto(p, asOf)),
            needsAttention = subs.Where(x => x.Sub.Status == SubtaskStatus.Blocked || (x.Sub.Status != SubtaskStatus.Done && x.Sub.DueDate < asOf))
                .Select(x => new { subtask = new SubtaskDto(x.Sub.Id, x.Sub.TaskId, x.Sub.Title, x.Sub.OwnerId, x.Sub.Owner?.Name ?? "", x.Sub.DueDate, x.Sub.Status.ToString(), x.Sub.BlockerNote), taskTitle = x.Task.Title, projectName = x.Project.Name })
        });
    }

    [HttpGet("api/mywork")]
    public async Task<IActionResult> MyWork()
    {
        var uid = Authz.UserId(User);
        var subs = await _db.Subtasks.Include(s => s.Task).ThenInclude(t => t.Project).Include(s => s.Owner)
            .Where(s => s.OwnerId == uid).OrderBy(s => s.DueDate).ToListAsync();
        return Ok(subs.Select(s => new { subtask = new SubtaskDto(s.Id, s.TaskId, s.Title, s.OwnerId, s.Owner?.Name ?? "", s.DueDate, s.Status.ToString(), s.BlockerNote), taskTitle = s.Task?.Title, projectName = s.Task?.Project?.Name }));
    }
}
