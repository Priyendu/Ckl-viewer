# Ckl-viewer

A Windows desktop viewer/editor for DISA STIG checklists, inspired by
[cyber.trackr.live/ckl-viewer](https://cyber.trackr.live/ckl-viewer), with
Vulnerator-style Excel reporting.

Built with C# / .NET 8 / WPF.

## Features

- **Open `.ckl` (XML) and `.cklb` (JSON)** — both DISA STIG Viewer 2.x and 3.x
  checklist formats, via the Open button, drag-and-drop onto the window, or
  command-line arguments.
- **Multi-checklist sessions (Vulnerator-style)** — multi-select several
  checklists in the Open dialog (or drop them all at once); findings from every
  asset are aggregated into one view, one summary, and one report.
- **Browse and filter findings** — search across rule text, filter by status,
  severity (CAT I/II/III), STIG, and asset; color-coded statuses.
- **Edit inline** — status, finding details, comments, severity override with
  justification, and target/host information; live summary counts.
- **Apply SCAP scan results** — load an XCCDF result file and statuses are
  updated automatically by rule version (`pass` → Not a Finding, `fail` → Open,
  `notapplicable` → Not Applicable), with an audit note appended to finding
  details.
- **Save / convert** — write back as `.ckl` or export as `.cklb`, so the file
  opens natively in STIG Viewer 2.x or 3.x.
- **Vulnerator-style Excel reports** — one click generates an `.xlsx` workbook
  with three sheets:
  - *Executive Summary* — per-STIG counts by status and severity plus compliance %
  - *POA&M* — eMASS-style Plan of Action & Milestones rows for every Open /
    Not Reviewed finding, severity color-coded
  - *Vulnerability Details* — the full checklist with rule text, CCIs, finding
    details, and comments, with auto-filter enabled

> **Handling note:** like the original web viewer, this tool is for
> unclassified data only. Handle checklists containing classified information
> or CUI on an authorized system.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
dotnet build CklViewer.sln
dotnet test CklViewer.sln
dotnet run --project src/CklViewer
```

To produce a standalone executable:

```powershell
dotnet publish src/CklViewer -c Release -r win-x64 --self-contained false
```

## Project layout

```
src/CklViewer/
  Models/       Checklist, STIG, and vulnerability domain models
  Parsing/      CKL (XML), CKLB (JSON), and XCCDF result parsers
  Writing/      CKL and CKLB writers
  Reports/      Excel report generator (ClosedXML)
  ViewModels/   WPF MVVM layer
tests/CklViewer.Tests/  Round-trip, SCAP, and report unit tests
```
