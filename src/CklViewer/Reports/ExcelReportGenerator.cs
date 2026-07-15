using System.IO;
using ClosedXML.Excel;
using CklViewer.Models;

namespace CklViewer.Reports;

/// <summary>
/// Generates a Vulnerator-style Excel workbook from one or more checklists:
/// Executive Summary, POA&amp;M, and Vulnerability Details sheets.
/// </summary>
public static class ExcelReportGenerator
{
    private static readonly XLColor HeaderFill = XLColor.FromArgb(31, 78, 121);
    private static readonly XLColor CatIFill = XLColor.FromArgb(192, 0, 0);
    private static readonly XLColor CatIiFill = XLColor.FromArgb(237, 125, 49);
    private static readonly XLColor CatIiiFill = XLColor.FromArgb(255, 192, 0);

    public static void WriteReport(IReadOnlyList<ChecklistDocument> documents, string path)
    {
        using var workbook = new XLWorkbook();
        BuildSummarySheet(workbook, documents);
        BuildPoamSheet(workbook, documents);
        BuildDetailsSheet(workbook, documents);
        workbook.SaveAs(path);
    }

    private static void BuildSummarySheet(XLWorkbook workbook, IReadOnlyList<ChecklistDocument> documents)
    {
        var sheet = workbook.Worksheets.Add("Executive Summary");
        sheet.Cell(1, 1).Value = "Ckl-viewer Executive Summary";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}";
        sheet.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        var headers = new[]
        {
            "Asset", "STIG", "Version / Release", "Total",
            "Open CAT I", "Open CAT II", "Open CAT III",
            "Not a Finding", "Not Applicable", "Not Reviewed", "Compliance %"
        };

        const int startRow = 4;
        for (var i = 0; i < headers.Length; i++)
        {
            StyleHeader(sheet.Cell(startRow, i + 1), headers[i]);
        }

        var row = startRow + 1;
        foreach (var document in documents)
        {
            var assetName = string.IsNullOrWhiteSpace(document.Asset.HostName)
                ? document.Title ?? "(unnamed asset)"
                : document.Asset.HostName;

            foreach (var stig in document.Stigs)
            {
                var vulns = stig.Vulnerabilities;
                int Count(Func<Vulnerability, bool> predicate) => vulns.Count(predicate);

                var total = vulns.Count;
                var open = Count(v => v.Status == FindingStatus.Open);
                var naf = Count(v => v.Status == FindingStatus.NotAFinding);
                var na = Count(v => v.Status == FindingStatus.NotApplicable);
                var nr = Count(v => v.Status == FindingStatus.NotReviewed);
                var evaluated = total - na;

                sheet.Cell(row, 1).Value = assetName;
                sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(stig.Title) ? stig.StigId : stig.Title;
                sheet.Cell(row, 3).Value = $"V{stig.Version} {stig.ReleaseInfo}".Trim();
                sheet.Cell(row, 4).Value = total;
                sheet.Cell(row, 5).Value = Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.High);
                sheet.Cell(row, 6).Value = Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.Medium);
                sheet.Cell(row, 7).Value = Count(v => v.Status == FindingStatus.Open && v.EffectiveSeverity == Severity.Low);
                sheet.Cell(row, 8).Value = naf;
                sheet.Cell(row, 9).Value = na;
                sheet.Cell(row, 10).Value = nr;
                sheet.Cell(row, 11).Value = evaluated > 0 ? Math.Round(100.0 * naf / evaluated, 1) : 0;

                if (open > 0)
                {
                    sheet.Range(row, 5, row, 7).Style.Font.SetBold();
                }

                row++;
            }
        }

        sheet.Range(startRow, 1, Math.Max(row - 1, startRow), headers.Length)
            .Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Hair);
        sheet.Columns().AdjustToContents(startRow, row);
        sheet.SheetView.FreezeRows(startRow);

        BuildStatusLegend(sheet, row + 2);
    }

    private static void BuildStatusLegend(IXLWorksheet sheet, int row)
    {
        sheet.Cell(row, 1).Value = "Severity legend:";
        sheet.Cell(row, 1).Style.Font.SetBold();
        var legend = new[] { ("CAT I (high)", CatIFill), ("CAT II (medium)", CatIiFill), ("CAT III (low)", CatIiiFill) };
        for (var i = 0; i < legend.Length; i++)
        {
            var cell = sheet.Cell(row, 2 + i);
            cell.Value = legend[i].Item1;
            cell.Style.Fill.SetBackgroundColor(legend[i].Item2);
            cell.Style.Font.SetFontColor(XLColor.White).Font.SetBold();
        }
    }

    private static void BuildPoamSheet(XLWorkbook workbook, IReadOnlyList<ChecklistDocument> documents)
    {
        var sheet = workbook.Worksheets.Add("POA&M");
        var headers = new[]
        {
            "Control Vulnerability Description", "Security Checks", "Raw Severity",
            "Mitigations", "Severity", "Relevance of Threat", "Likelihood", "Impact",
            "Residual Risk Level", "Status", "Devices Affected", "Source Identifying Vulnerability",
            "CCI References", "Resources Required", "Scheduled Completion Date",
            "Milestone with Completion Dates", "Comments"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            StyleHeader(sheet.Cell(1, i + 1), headers[i]);
        }

        var row = 2;
        foreach (var document in documents)
        {
            var device = string.IsNullOrWhiteSpace(document.Asset.HostName)
                ? document.Title ?? "(unnamed asset)"
                : document.Asset.HostName;

            foreach (var stig in document.Stigs)
            {
                foreach (var vuln in stig.Vulnerabilities.Where(v =>
                             v.Status is FindingStatus.Open or FindingStatus.NotReviewed))
                {
                    var category = vuln.Category;
                    sheet.Cell(row, 1).Value = $"{vuln.RuleTitle}\n\n{Truncate(vuln.Discussion, 800)}";
                    sheet.Cell(row, 2).Value = $"{vuln.VulnId} / {vuln.RuleVersion}";
                    sheet.Cell(row, 3).Value = category;
                    sheet.Cell(row, 4).Value = vuln.Mitigations;
                    sheet.Cell(row, 5).Value = category;
                    sheet.Cell(row, 6).Value = "Medium";
                    sheet.Cell(row, 7).Value = "Medium";
                    sheet.Cell(row, 8).Value = category switch { "CAT I" => "High", "CAT III" => "Low", _ => "Medium" };
                    sheet.Cell(row, 9).Value = category switch { "CAT I" => "High", "CAT III" => "Low", _ => "Medium" };
                    sheet.Cell(row, 10).Value = vuln.Status == FindingStatus.Open ? "Ongoing" : "Not Reviewed";
                    sheet.Cell(row, 11).Value = device;
                    sheet.Cell(row, 12).Value = string.IsNullOrWhiteSpace(vuln.StigRef) ? stig.Title : vuln.StigRef;
                    sheet.Cell(row, 13).Value = vuln.CciDisplay;
                    sheet.Cell(row, 17).Value = vuln.Comments;

                    ApplySeverityFill(sheet.Cell(row, 3), vuln.EffectiveSeverity);
                    row++;
                }
            }
        }

        FinishTableSheet(sheet, row, headers.Length, wrapColumns: new[] { 1, 4, 16, 17 }, wideColumns: new[] { 1 });
    }

    private static void BuildDetailsSheet(XLWorkbook workbook, IReadOnlyList<ChecklistDocument> documents)
    {
        var sheet = workbook.Worksheets.Add("Vulnerability Details");
        var headers = new[]
        {
            "Asset", "STIG", "Vuln ID", "Rule ID", "Rule Version", "Severity", "Severity Override",
            "Status", "Rule Title", "Discussion", "Check Content", "Fix Text",
            "CCI References", "Finding Details", "Comments"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            StyleHeader(sheet.Cell(1, i + 1), headers[i]);
        }

        var row = 2;
        foreach (var document in documents)
        {
            var assetName = string.IsNullOrWhiteSpace(document.Asset.HostName)
                ? document.Title ?? "(unnamed asset)"
                : document.Asset.HostName;

            foreach (var stig in document.Stigs)
            {
                foreach (var vuln in stig.Vulnerabilities)
                {
                    sheet.Cell(row, 1).Value = assetName;
                    sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(stig.Title) ? stig.StigId : stig.Title;
                    sheet.Cell(row, 3).Value = vuln.VulnId;
                    sheet.Cell(row, 4).Value = vuln.RuleId;
                    sheet.Cell(row, 5).Value = vuln.RuleVersion;
                    sheet.Cell(row, 6).Value = vuln.Category;
                    sheet.Cell(row, 7).Value = string.IsNullOrWhiteSpace(vuln.SeverityOverride)
                        ? string.Empty
                        : Severity.ToCategory(vuln.SeverityOverride);
                    sheet.Cell(row, 8).Value = vuln.Status.ToDisplayString();
                    sheet.Cell(row, 9).Value = vuln.RuleTitle;
                    sheet.Cell(row, 10).Value = Truncate(vuln.Discussion, 2000);
                    sheet.Cell(row, 11).Value = Truncate(vuln.CheckContent, 2000);
                    sheet.Cell(row, 12).Value = Truncate(vuln.FixText, 2000);
                    sheet.Cell(row, 13).Value = vuln.CciDisplay;
                    sheet.Cell(row, 14).Value = Truncate(vuln.FindingDetails, 2000);
                    sheet.Cell(row, 15).Value = Truncate(vuln.Comments, 2000);

                    ApplySeverityFill(sheet.Cell(row, 6), vuln.EffectiveSeverity);
                    if (vuln.Status == FindingStatus.Open)
                    {
                        sheet.Cell(row, 8).Style.Font.SetFontColor(XLColor.Red).Font.SetBold();
                    }

                    row++;
                }
            }
        }

        FinishTableSheet(sheet, row, headers.Length, wrapColumns: new[] { 9, 10, 11, 12, 14, 15 }, wideColumns: new[] { 9, 10, 11, 12 });
        sheet.RangeUsed()?.SetAutoFilter();
    }

    private static void FinishTableSheet(IXLWorksheet sheet, int nextRow, int columnCount, int[] wrapColumns, int[] wideColumns)
    {
        if (nextRow > 2)
        {
            sheet.Range(1, 1, nextRow - 1, columnCount)
                .Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                .Border.SetInsideBorder(XLBorderStyleValues.Hair);
        }

        sheet.Columns(1, columnCount).AdjustToContents(1, Math.Min(nextRow, 50));
        foreach (var column in wrapColumns)
        {
            sheet.Column(column).Style.Alignment.SetWrapText(true);
        }

        foreach (var column in wideColumns)
        {
            sheet.Column(column).Width = 60;
        }

        sheet.Rows(2, Math.Max(nextRow - 1, 2)).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        sheet.SheetView.FreezeRows(1);
    }

    private static void StyleHeader(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        cell.Style.Fill.SetBackgroundColor(HeaderFill);
        cell.Style.Alignment.SetWrapText(true);
        cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
    }

    private static void ApplySeverityFill(IXLCell cell, string severity)
    {
        var fill = Severity.Normalize(severity) switch
        {
            Severity.High => CatIFill,
            Severity.Low => CatIiiFill,
            _ => CatIiFill
        };
        cell.Style.Fill.SetBackgroundColor(fill);
        cell.Style.Font.SetFontColor(XLColor.White).Font.SetBold();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + " …";
}
