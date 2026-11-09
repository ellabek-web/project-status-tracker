using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectStatusTracker.Server.Data;

namespace ProjectStatusTracker.Server.Services;

/// <summary>Role / progress rules, enforced server-side. UI labels mirror these.</summary>
public static class Authz
{
    public static readonly Role[] ViewOnly = { Role.President, Role.EVP, Role.VP, Role.Director };
    public static bool IsViewOnly(Role r) => ViewOnly.Contains(r);
    public static int UserId(ClaimsPrincipal u) => int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Role UserRole(ClaimsPrincipal u) => Enum.Parse<Role>(u.FindFirstValue(ClaimTypes.Role)!);
    public static bool CanEditWork(Role r) => r is Role.Manager or Role.ScrumMaster or Role.Developer;
}

public static class Progress
{
    public static int Percent(int done, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * done / total);

    /// <summary>Delayed if any subtask is blocked or overdue (due before <paramref name="asOf"/> and not done);
    /// AtRisk if any subtask is in progress and past half its window is not tracked, so: any overdue OR blocked
    /// is Delayed, otherwise OnTrack. Kept simple and deterministic, matching the prototype rule.</summary>
    public static string Health(IEnumerable<Subtask> subs, DateOnly asOf)
    {
        var list = subs.ToList();
        if (list.Any(s => s.Status == SubtaskStatus.Blocked)) return "Delayed";
        if (list.Any(s => s.Status != SubtaskStatus.Done && s.DueDate < asOf)) return "Delayed";
        if (list.Any(s => s.Status == SubtaskStatus.InProgress && s.DueDate < asOf.AddDays(3))) return "AtRisk";
        return "OnTrack";
    }

    public static string Fmt(Subtask s, string taskTitle, string projectName) =>
        $"{s.Title} — {taskTitle} ({projectName}) · {s.Owner?.Name} · due {s.DueDate:yyyy-MM-dd}" +
        (s.Status == SubtaskStatus.Blocked && !string.IsNullOrWhiteSpace(s.BlockerNote) ? $" · {s.BlockerNote}" : "");
}

public static class ReportBuilder
{
    public static Report Build(AppDbContext db, int? projectId, Cadence cadence, DateOnly periodEnd)
    {
        var periodStart = cadence == Cadence.Weekly ? periodEnd.AddDays(-6) : periodEnd.AddDays(-13);
        var projects = db.Projects.Include(p => p.Tasks).ThenInclude(t => t.Subtasks).ThenInclude(s => s.Owner)
            .Where(p => projectId == null || p.Id == projectId).ToList();
        var allSubs = projects.SelectMany(p => p.Tasks.SelectMany(t => t.Subtasks.Select(s => (Sub: s, Task: t, Project: p)))).ToList();
        var done = allSubs.Where(x => x.Sub.Status == SubtaskStatus.Done).ToList();
        var inProg = allSubs.Where(x => x.Sub.Status == SubtaskStatus.InProgress).ToList();
        var blocked = allSubs.Where(x => x.Sub.Status == SubtaskStatus.Blocked).ToList();
        var notDone = allSubs.Where(x => x.Sub.Status != SubtaskStatus.Done).OrderBy(x => x.Sub.DueDate).ToList();
        var overdue = notDone.Where(x => x.Sub.DueDate < periodEnd).ToList();
        var total = allSubs.Count;
        var pct = Progress.Percent(done.Count, total);
        var scope = projectId == null ? "All projects" : projects.FirstOrDefault()?.Name ?? "Project";
        var title = projectId == null
            ? $"All projects — {cadence} report ({periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd})"
            : $"{scope} — {cadence} Status";
        var summary = $"{projects.Count} project(s) · {projects.Sum(p => p.Tasks.Count)} tasks · {total} subtasks · {pct}% complete overall. " +
            $"In this {cadence.ToString().ToLower()} period ({periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}), {done.Count} subtasks were completed, {inProg.Count} are in progress, {blocked.Count} blocked and {overdue.Count} overdue. Progress is calculated from completed subtasks, not estimated.";
        var risks = new List<string>();
        if (blocked.Count > 0) risks.Add($"{blocked.Count} blocked subtask(s) need unblocking — see Blocked section.");
        if (overdue.Count > 0) risks.Add($"{overdue.Count} subtask(s) overdue. Oldest: {overdue.OrderBy(x => x.Sub.DueDate).First().Sub.Title}.");
        if (risks.Count == 0) risks.Add("No blockers or overdue work in this period.");

        var slides = new List<SlideDto>
        {
            new($"{title}", new[] { scope, $"Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd} · {cadence}", summary }),
            new("Summary", new[] { summary }),
            new("Progress by project", projects.Select(p => { var ps = p.Tasks.SelectMany(t => t.Subtasks).ToList(); return $"{p.Name}: {Progress.Percent(ps.Count(s => s.Status == SubtaskStatus.Done), ps.Count)}% ({ps.Count(s => s.Status == SubtaskStatus.Done)}/{ps.Count} subtasks done)"; }).ToArray()),
            new("Completed in this period", done.Select(x => Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name)).ToArray()),
            new("In progress & planned", inProg.Select(x => Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name)).ToArray()),
            new("Blockers & risks", blocked.Select(x => Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name)).Concat(risks).ToArray()),
            new("Next steps", notDone.Select(x => Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name)).ToArray()),
        };
        return new Report
        {
            ProjectId = projectId,
            Title = title,
            Cadence = cadence,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Summary = summary,
            CompletedText = string.Join("\n", done.Select(x => "• " + Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name))),
            InProgressText = string.Join("\n", inProg.Select(x => "• " + Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name))),
            BlockedText = string.Join("\n", blocked.Select(x => "• " + Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name))),
            RisksText = string.Join("\n", risks.Select(r => "• " + r)),
            NextStepsText = string.Join("\n", notDone.Select(x => "• " + Progress.Fmt(x.Sub, x.Task.Title, x.Project.Name))),
            SlidesJson = JsonSerializer.Serialize(slides),
            Status = ReportStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }
}
