using System;
using System.IO;

namespace AiUsageTrainer.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AiUsageTrainer");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string ReportsDir => Path.Combine(Root, "reports");
    public static string WorkspacesDir => Path.Combine(Root, "workspaces");

    /// <summary>
    /// Empty scratch folder used as the CLI's working directory for eval turns (problem
    /// generation, evaluation) so it never picks up a CLAUDE.md or project settings from
    /// wherever the app happens to be launched.
    /// </summary>
    public static string CliWorkDir => Path.Combine(Root, "cli-workdir");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ReportsDir);
        Directory.CreateDirectory(WorkspacesDir);
        Directory.CreateDirectory(CliWorkDir);
    }
}
