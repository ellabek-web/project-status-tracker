using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectStatusTracker.Server.Data;
using ProjectStatusTracker.Server.Services;

namespace ProjectStatusTracker.Server.Controllers;

[ApiController, Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    private static ReportDto ToDto(Report r) => new(r.Id, r.ProjectId, r.Title, r.Cadence.ToString(), r.PeriodStart, r.PeriodEnd,
        r.Summary, r.CompletedText, r.InProgressText, r.BlockedText, r.RisksText, r.NextStepsText, r.Status.ToString(), r.CreatedAt, r.SlidesJson);

    [HttpPost("api/reports/generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateReportRequest req)
    {
        var role = Authz.UserRole(User);
        if (role is not (Role.Manager or Role.ScrumMaster)) return Forbid();
        if (req.ProjectId != null && !await _db.Projects.AnyAsync(p => p.Id == req.ProjectId)) return NotFound(new { error = "Project not found." });
        var report = ReportBuilder.Build(_db, req.ProjectId, req.Cadence, req.PeriodEnd);
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = report.Id }, ToDto(report));
    }

    [HttpGet("api/reports")]
    public async Task<IActionResult> List() =>
        Ok(await _db.Reports.OrderByDescending(r => r.CreatedAt).Select(r => ToDto(r)).ToListAsync());

    [HttpGet("api/reports/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await _db.Reports.FindAsync(id);
        if (r == null) return NotFound();
        return Ok(ToDto(r));
    }

    [HttpPut("api/reports/{id:int}/review")]
    public async Task<IActionResult> Review(int id)
    {
        if (Authz.UserRole(User) != Role.Manager) return Forbid(); // Managers only approve/review
        var r = await _db.Reports.FindAsync(id);
        if (r == null) return NotFound();
        r.Status = ReportStatus.Reviewed;
        await _db.SaveChangesAsync();
        return Ok(ToDto(r));
    }

    [HttpGet("api/reports/{id:int}/download")]
    public async Task<IActionResult> Download(int id, [FromQuery] string format = "xlsx")
    {
        var r = await _db.Reports.FindAsync(id);
        if (r == null) return NotFound();
        var f = format.ToLowerInvariant();
        return f switch
        {
            "xlsx" => File(ExportService.Excel(_db, r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"report-{id}.xlsx"),
            "ppt" or "pptx" => File(ExportService.Pptx(r), "application/vnd.openxmlformats-officedocument.presentationml.presentation", $"report-{id}.pptx"),
            "pdf" => File(ExportService.Pdf(r), "application/pdf", $"report-{id}.pdf"),
            _ => BadRequest(new { error = "format must be pdf, pptx or xlsx." })
        };
    }

    [HttpGet("api/reports/{id:int}/slides")]
    public async Task<IActionResult> Slides(int id)
    {
        var r = await _db.Reports.FindAsync(id);
        if (r == null) return NotFound();
        return Ok(JsonSerializer.Deserialize<List<SlideDto>>(r.SlidesJson) ?? new());
    }
}
