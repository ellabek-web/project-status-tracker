using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.EntityFrameworkCore;
using ProjectStatusTracker.Server.Data;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace ProjectStatusTracker.Server.Services;

/// <summary>Real file exports: .xlsx via ClosedXML, .pptx via OpenXML, .pdf via a minimal PDF writer. No fake downloads.</summary>
public static class ExportService
{
    public static byte[] Excel(AppDbContext db, Report r)
    {
        using var wb = new XLWorkbook();
        var s = wb.Worksheets.Add("Report");
        s.Cell(1, 1).Value = r.Title; s.Cell(1, 1).Style.Font.Bold = true; s.Cell(1, 1).Style.Font.FontSize = 14;
        s.Cell(2, 1).Value = r.Summary;
        s.Cell(4, 1).Value = "Completed"; s.Cell(4, 2).Value = r.CompletedText;
        s.Cell(5, 1).Value = "In progress"; s.Cell(5, 2).Value = r.InProgressText;
        s.Cell(6, 1).Value = "Blocked"; s.Cell(6, 2).Value = r.BlockedText;
        s.Cell(7, 1).Value = "Risks"; s.Cell(7, 2).Value = r.RisksText;
        s.Cell(8, 1).Value = "Next steps"; s.Cell(8, 2).Value = r.NextStepsText;
        s.Columns().AdjustToContents();

        var d = wb.Worksheets.Add("Subtasks");
        d.Cell(1, 1).Value = "Project"; d.Cell(1, 2).Value = "Task"; d.Cell(1, 3).Value = "Subtask";
        d.Cell(1, 4).Value = "Owner"; d.Cell(1, 5).Value = "Due date"; d.Cell(1, 6).Value = "Status"; d.Cell(1, 7).Value = "Blocker note";
        var row = 2;
        var subs = db.Subtasks.Include(x => x.Task).ThenInclude(t => t.Project).Include(x => x.Owner)
            .Where(x => r.ProjectId == null || x.Task!.ProjectId == r.ProjectId)
            .OrderBy(x => x.DueDate).ToList();
        foreach (var st in subs)
        {
            d.Cell(row, 1).Value = st.Task?.Project?.Name ?? "";
            d.Cell(row, 2).Value = st.Task?.Title ?? "";
            d.Cell(row, 3).Value = st.Title;
            d.Cell(row, 4).Value = st.Owner?.Name ?? "";
            d.Cell(row, 5).Value = st.DueDate.ToString("yyyy-MM-dd");
            d.Cell(row, 6).Value = st.Status.ToString();
            d.Cell(row, 7).Value = st.BlockerNote;
            row++;
        }
        d.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] Pptx(Report r)
    {
        var slides = JsonSerializer.Deserialize<List<SlideDto>>(r.SlidesJson) ?? new();
        using var ms = new MemoryStream();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presPart = doc.AddPresentationPart();
            presPart.Presentation = new P.Presentation(
                new P.SlideMasterIdList(new P.SlideMasterId { RelationshipId = "rId1", Id = 1U }),
                new P.SlideIdList());
            var masterPart = presPart.AddNewPart<SlideMasterPart>("rId1");
            masterPart.SlideMaster = new P.SlideMaster(
                new P.CommonSlideData(new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup { Offset = new A.Offset { X = 0, Y = 0 }, Extents = new A.Extents { Cx = 9144000, Cy = 6858000 }, ChildOffset = new A.ChildOffset { X = 0, Y = 0 }, ChildExtents = new A.ChildExtents { Cx = 9144000, Cy = 6858000 } }))));
            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
            layoutPart.SlideLayout = new P.SlideLayout(
                new P.CommonSlideData(new P.ShapeTree(
                    new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                        new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                    new P.GroupShapeProperties(new A.TransformGroup { Offset = new A.Offset { X = 0, Y = 0 }, Extents = new A.Extents { Cx = 9144000, Cy = 6858000 }, ChildOffset = new A.ChildOffset { X = 0, Y = 0 }, ChildExtents = new A.ChildExtents { Cx = 9144000, Cy = 6858000 } }))),
                new P.ColorMapOverride(new A.MasterColorMapping()));
            masterPart.SlideMaster.SlideLayoutIdList = new P.SlideLayoutIdList(new P.SlideLayoutId { RelationshipId = "rId1", Id = 1U });

            uint slideId = 256;
            foreach (var (slide, i) in slides.Select((s, i) => (s, i)))
            {
                var slidePart = presPart.AddNewPart<SlidePart>();
                slidePart.AddPart(layoutPart);
                slidePart.Slide = BuildSlide(slide, i == 0);
                presPart.Presentation.SlideIdList!.Append(new P.SlideId { RelationshipId = presPart.GetIdOfPart(slidePart), Id = slideId++ });
            }
            presPart.Presentation.Save();
        }
        return ms.ToArray();
    }

    private static P.Slide BuildSlide(SlideDto slide, bool cover)
    {
        // Gold D4AF37, purple 6D28D9, black 0B0B10 theme.
        var bg = cover ? "0B0B10" : "FFFFFF";
        var titleColor = cover ? "D4AF37" : "6D28D9";
        var bodyColor = cover ? "FFFFFF" : "0B0B10";
        var tree = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = "" },
                new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup
            {
                Offset = new A.Offset { X = 0, Y = 0 },
                Extents = new A.Extents { Cx = 9144000, Cy = 6858000 },
                ChildOffset = new A.ChildOffset { X = 0, Y = 0 },
                ChildExtents = new A.ChildExtents { Cx = 9144000, Cy = 6858000 }
            }));
        tree.Append(TextBox(2U, slide.Title, titleColor, 28, 400000, cover ? 2500000 : 300000, 8344000, 900000));
        var y = cover ? 3600000L : 1400000L;
        foreach (var b in slide.Bullets.Take(10))
        {
            tree.Append(TextBox(100U + (uint)y, "•  " + b, bodyColor, 16, 400000, y, 8344000, 500000));
            y += 550000;
        }
        // Gold accent bar under the title
        tree.Append(Shape(999U, "D4AF37", 400000, cover ? 3400000 : 1250000, 1200000, 60000));
        return new P.Slide(new P.CommonSlideData(
            new P.Background(new P.BackgroundProperties(new A.SolidFill(new A.RgbColorModelHex { Val = bg }))),
            tree));
    }

    private static P.Shape TextBox(uint id, string text, string color, int fontSize, long x, long y, long w, long h)
    {
        var paragraphs = text.Split('\n').Select(line => new A.Paragraph(
            new A.Run(new A.RunProperties { Language = "en-GB", FontSize = fontSize * 100 },
                new A.SolidFill(new A.RgbColorModelHex { Val = color }),
                new A.Text(line)))).ToArray();
        var body = new P.TextBody(new A.BodyProperties { Wrap = A.TextWrappingValues.Square }, new A.ListStyle());
        foreach (var para in paragraphs) body.Append(para);
        return new P.Shape(
            new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = $"Text{id}" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true })),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = w, Cy = h })),
            body);
    }

    private static P.Shape Shape(uint id, string color, long x, long y, long w, long h)
    {
        var geom = new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle };
        return new P.Shape(
            new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Bar" },
                new P.NonVisualShapeDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = w, Cy = h }),
                geom,
                new A.SolidFill(new A.RgbColorModelHex { Val = color })),
            new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph()));
    }

    // ---- Minimal valid PDF (Helvetica, one page per ~45 lines) ----
    public static byte[] Pdf(Report r)
    {
        var lines = new List<string> { r.Title, "", r.Summary, "", "Completed:", r.CompletedText, "", "In progress:", r.InProgressText, "", "Blocked:", r.BlockedText, "", "Risks:", r.RisksText, "", "Next steps:", r.NextStepsText };
        var wrapped = lines.SelectMany(l => Wrap(l, 95)).ToList();
        var pages = wrapped.Chunk(45).ToList();
        var objects = new List<string>();
        // 1 catalog, 2 pages, then per page: page obj + content obj, font last
        var fontId = 3 + pages.Count * 2;
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => $"{3 + i * 2} 0 R"))}] /Count {pages.Count} >>");
        for (var i = 0; i < pages.Count; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {4 + i * 2} 0 R >>");
            var sb = new StringBuilder("BT /F1 11 Tf 40 800 Td 14 TL\n");
            foreach (var line in pages[i]) sb.Append($"({Escape(line)}) Tj T*\n");
            sb.Append("ET");
            var content = sb.ToString();
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var out_ = new MemoryStream();
        var offsets = new List<long>();
        void Write(string s) { var b = Encoding.ASCII.GetBytes(s); out_.Write(b, 0, b.Length); }
        Write("%PDF-1.4\n");
        for (var i = 0; i < objects.Count; i++) { offsets.Add(out_.Position); Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = out_.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Write($"{o:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return out_.ToArray();
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")
        .Replace("—", "-").Replace("•", "-");
    private static IEnumerable<string> Wrap(string line, int width)
    {
        if (string.IsNullOrWhiteSpace(line)) { yield return ""; yield break; }
        var words = line.Split(' ');
        var cur = "";
        foreach (var w in words)
        {
            if ((cur + " " + w).Trim().Length > width) { yield return cur.Trim(); cur = w; }
            else cur += " " + w;
        }
        if (cur.Trim().Length > 0) yield return cur.Trim();
    }
}
