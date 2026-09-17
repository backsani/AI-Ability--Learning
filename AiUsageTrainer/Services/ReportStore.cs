using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AiUsageTrainer.Models;

namespace AiUsageTrainer.Services;

public sealed class ReportStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Keep Korean readable if the user opens the file by hand.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string Save(PracticeReport report)
    {
        AppPaths.EnsureCreated();
        var path = PathFor(report);
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options), Common.Utf8.NoBom);
        return path;
    }

    public List<PracticeReport> LoadAll()
    {
        AppPaths.EnsureCreated();
        var list = new List<PracticeReport>();

        foreach (var file in Directory.EnumerateFiles(AppPaths.ReportsDir, "*.json"))
        {
            try
            {
                var report = JsonSerializer.Deserialize<PracticeReport>(File.ReadAllText(file));
                if (report is not null) list.Add(report);
            }
            catch (Exception)
            {
                // Skip anything that is not a readable report rather than failing the whole list.
            }
        }

        return list.OrderByDescending(r => r.CreatedAt).ToList();
    }

    public void Delete(PracticeReport report)
    {
        var path = PathFor(report);
        if (File.Exists(path)) File.Delete(path);
    }

    public string ReportsDirectory => AppPaths.ReportsDir;

    private static string PathFor(PracticeReport report)
        => Path.Combine(AppPaths.ReportsDir, $"{report.CreatedAt:yyyyMMdd_HHmmss}_{report.Id[..8]}.json");
}
