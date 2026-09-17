using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AiUsageTrainer.Services;

public sealed record WorkspaceFileInfo(string RelativePath, long SizeBytes, DateTime ModifiedAt);

/// <summary>
/// Owns the per-session scratch folder the collaborator CLI is confined to (its process working
/// directory) while it actually reads/writes/executes code for the problem being practiced.
/// </summary>
public static class WorkspaceManager
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".vscode", ".idea", "__pycache__", ".venv"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cpp", ".c", ".h", ".hpp", ".java", ".py", ".js", ".ts", ".tsx", ".jsx",
        ".json", ".md", ".txt", ".yml", ".yaml", ".xml", ".html", ".css", ".sql", ".csv",
        ".sh", ".ps1", ".csproj", ".gitignore", ".cfg", ".ini", ".toml"
    };

    public static string CreateNew()
    {
        AppPaths.EnsureCreated();
        var id = Guid.NewGuid().ToString("N")[..12];
        var path = Path.Combine(AppPaths.WorkspacesDir, id);
        Directory.CreateDirectory(path);
        return path;
    }

    public static void WriteStarterFile(string workspacePath, string relativePath, string content)
    {
        var safe = relativePath.Replace("..", "").TrimStart('/', '\\');
        if (safe.Length == 0) return;

        var full = Path.Combine(workspacePath, safe);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(full, content, Common.Utf8.NoBom);
    }

    public static List<WorkspaceFileInfo> ListFiles(string workspacePath, int maxFiles = 200)
    {
        var result = new List<WorkspaceFileInfo>();
        if (!Directory.Exists(workspacePath)) return result;

        void Walk(string dir)
        {
            if (result.Count >= maxFiles) return;

            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (Exception) { return; }

            foreach (var entry in entries)
            {
                if (result.Count >= maxFiles) return;

                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (SkipDirs.Contains(name)) continue;
                    Walk(entry);
                }
                else
                {
                    try
                    {
                        var info = new FileInfo(entry);
                        var rel = Path.GetRelativePath(workspacePath, entry).Replace('\\', '/');
                        result.Add(new WorkspaceFileInfo(rel, info.Length, info.LastWriteTime));
                    }
                    catch (Exception) { /* skip unreadable entry */ }
                }
            }
        }

        Walk(workspacePath);
        return result.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// A text snapshot of the workspace for the evaluator, which runs with no tools and so
    /// cannot browse the files itself: a file tree plus the contents of small text files.
    /// </summary>
    public static string BuildSnapshot(string workspacePath, int maxTotalChars = 40_000, int maxFileChars = 6_000)
    {
        var files = ListFiles(workspacePath);
        if (files.Count == 0) return "(작업 폴더가 비어 있습니다. 결과물을 만들지 않았습니다.)";

        var sb = new StringBuilder();
        sb.AppendLine($"작업 폴더 파일 {files.Count}개:");
        foreach (var f in files) sb.AppendLine($"- {f.RelativePath} ({f.SizeBytes:N0} bytes)");
        sb.AppendLine();

        foreach (var f in files)
        {
            if (sb.Length >= maxTotalChars) { sb.AppendLine("(용량 제한으로 이하 파일 내용 생략)"); break; }
            if (!TextExtensions.Contains(Path.GetExtension(f.RelativePath))) continue;
            if (f.SizeBytes > 200_000) continue;

            string content;
            try { content = File.ReadAllText(Path.Combine(workspacePath, f.RelativePath)); }
            catch (Exception) { continue; }

            if (content.Length > maxFileChars)
                content = content[..maxFileChars] + "\n...(생략)...";

            sb.AppendLine($"### {f.RelativePath}");
            sb.AppendLine("```");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static void OpenInExplorer(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }
}
