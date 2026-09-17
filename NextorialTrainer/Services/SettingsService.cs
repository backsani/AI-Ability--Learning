using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using NextorialTrainer.Models;

namespace NextorialTrainer.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        AppPaths.EnsureCreated();
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // A corrupt settings file must not stop the app from starting.
            Current = new AppSettings();
        }

        FillDefaults();
    }

    private void FillDefaults()
    {
        foreach (var profile in CliAgentCatalog.Profiles)
        {
            var s = Current.AgentFor(profile.Id);

            if (string.IsNullOrWhiteSpace(s.ExecutablePath))
                s.ExecutablePath = CliAgentBackend.DetectPath(profile) ?? "";

            if (string.IsNullOrWhiteSpace(s.CollabModel))
                s.CollabModel = profile.DefaultCollabModel;

            if (string.IsNullOrWhiteSpace(s.EvalModel))
                s.EvalModel = profile.DefaultEvalModel;
        }
    }

    /// <summary>The active profile with the user's overrides applied.</summary>
    public CliAgentProfile ResolveProfile()
    {
        var preset = CliAgentCatalog.Find(Current.SelectedAgentId);
        return preset.WithOverrides(Current.AgentFor(preset.Id));
    }

    public AgentSettings ActiveAgent => Current.AgentFor(Current.SelectedAgentId);

    public void Save()
    {
        try
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(AppPaths.SettingsFile,
                JsonSerializer.Serialize(Current, Options), Common.Utf8.NoBom);
        }
        catch (Exception)
        {
            // Losing a preference is not worth crashing over.
        }
    }
}
