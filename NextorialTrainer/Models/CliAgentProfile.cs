using System;
using System.Collections.Generic;
using System.Linq;

namespace NextorialTrainer.Models;

/// <summary>How the conversation history is kept between turns.</summary>
public enum SessionMode
{
    /// <summary>The CLI stores the history; we resume it by id. Cheapest, but CLI-specific.</summary>
    Resume,

    /// <summary>We store the history and replay the whole transcript each turn. Works with any CLI.</summary>
    Replay
}

public enum ReplyFormat
{
    /// <summary>stdout is a JSON envelope; the reply lives in one field.</summary>
    Json,

    /// <summary>stdout is the reply text itself.</summary>
    Raw
}

public enum PromptVia
{
    Stdin,

    /// <summary>Passed as an argument via the {prompt} placeholder.</summary>
    Arg
}

/// <summary>
/// Describes how to drive one agent CLI. Built-in presets live in CliAgentCatalog; every field
/// can be overridden per machine from the settings screen, so a CLI that changes its flags can
/// be fixed without rebuilding the app.
///
/// Two purposes call into the same CLI with different flags: "eval" turns (problem generation,
/// evaluation) run with tools disabled, one shot, no file access. "collab" turns are the actual
/// practice conversation and run with tools enabled so the agent can read/write/run code in the
/// session's own workspace folder.
/// </summary>
public sealed class CliAgentProfile
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>True only for CLIs whose argument set was actually exercised by this app.</summary>
    public bool Verified { get; set; }

    /// <summary>True only for CLIs confirmed to actually read/write files as the collaborator.</summary>
    public bool CollabVerified { get; set; }

    /// <summary>Executable names looked for on PATH, plus absolute fallbacks.</summary>
    public List<string> ExecutableNames { get; set; } = new();
    public List<string> ExecutableFallbacks { get; set; } = new();

    public SessionMode SessionMode { get; set; } = SessionMode.Replay;
    public PromptVia PromptVia { get; set; } = PromptVia.Stdin;
    public ReplyFormat ReplyFormat { get; set; } = ReplyFormat.Raw;

    /// <summary>Field holding the reply text when ReplyFormat is Json.</summary>
    public string ReplyJsonField { get; set; } = "result";

    /// <summary>Optional boolean field that marks a failed turn.</summary>
    public string ErrorJsonField { get; set; } = "";

    /// <summary>Optional numeric field carrying the turn cost in USD.</summary>
    public string CostJsonField { get; set; } = "";

    /// <summary>
    /// Argument template used for eval turns (problem generation, evaluation): tools disabled,
    /// one shot. Placeholders: {sessionId} {model} {systemPrompt} {prompt}. Square brackets mark
    /// an optional group that is dropped when a placeholder inside is empty.
    /// </summary>
    public string FirstArgs { get; set; } = "";
    public string ResumeArgs { get; set; } = "";

    /// <summary>Argument template used for the practice (collaborator) conversation: tools enabled.</summary>
    public string CollabFirstArgs { get; set; } = "";
    public string CollabResumeArgs { get; set; } = "";

    /// <summary>True when the CLI takes the system prompt as a flag; otherwise it is folded into the prompt text.</summary>
    public bool SupportsSystemPromptFlag { get; set; }

    public List<string> Models { get; set; } = new();
    public string DefaultCollabModel { get; set; } = "";
    public string DefaultEvalModel { get; set; } = "";

    public CliAgentProfile Clone() => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        Description = Description,
        Verified = Verified,
        CollabVerified = CollabVerified,
        ExecutableNames = new List<string>(ExecutableNames),
        ExecutableFallbacks = new List<string>(ExecutableFallbacks),
        SessionMode = SessionMode,
        PromptVia = PromptVia,
        ReplyFormat = ReplyFormat,
        ReplyJsonField = ReplyJsonField,
        ErrorJsonField = ErrorJsonField,
        CostJsonField = CostJsonField,
        FirstArgs = FirstArgs,
        ResumeArgs = ResumeArgs,
        CollabFirstArgs = CollabFirstArgs,
        CollabResumeArgs = CollabResumeArgs,
        SupportsSystemPromptFlag = SupportsSystemPromptFlag,
        Models = new List<string>(Models),
        DefaultCollabModel = DefaultCollabModel,
        DefaultEvalModel = DefaultEvalModel
    };

    /// <summary>Applies the user's per-machine overrides on top of a built-in preset.</summary>
    public CliAgentProfile WithOverrides(AgentSettings s)
    {
        var p = Clone();

        if (!string.IsNullOrWhiteSpace(s.FirstArgs)) p.FirstArgs = s.FirstArgs;
        if (!string.IsNullOrWhiteSpace(s.ResumeArgs)) p.ResumeArgs = s.ResumeArgs;
        if (!string.IsNullOrWhiteSpace(s.CollabFirstArgs)) p.CollabFirstArgs = s.CollabFirstArgs;
        if (!string.IsNullOrWhiteSpace(s.CollabResumeArgs)) p.CollabResumeArgs = s.CollabResumeArgs;
        if (!string.IsNullOrWhiteSpace(s.ReplyJsonField)) p.ReplyJsonField = s.ReplyJsonField;

        if (Enum.TryParse<SessionMode>(s.SessionMode, true, out var sm)) p.SessionMode = sm;
        if (Enum.TryParse<ReplyFormat>(s.ReplyFormat, true, out var rf)) p.ReplyFormat = rf;
        if (Enum.TryParse<PromptVia>(s.PromptVia, true, out var pv)) p.PromptVia = pv;

        var models = SplitCsv(s.ModelsCsv);
        if (models.Count > 0) p.Models = models;

        return p;
    }

    public static List<string> SplitCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                 .Select(x => x.Trim())
                 .Where(x => x.Length > 0)
                 .ToList();
}

/// <summary>Per-machine settings for one agent. Empty strings mean "use the preset".</summary>
public sealed class AgentSettings
{
    public string ExecutablePath { get; set; } = "";
    public string CollabModel { get; set; } = "";
    public string EvalModel { get; set; } = "";
    public string ModelsCsv { get; set; } = "";
    public string FirstArgs { get; set; } = "";
    public string ResumeArgs { get; set; } = "";
    public string CollabFirstArgs { get; set; } = "";
    public string CollabResumeArgs { get; set; } = "";
    public string SessionMode { get; set; } = "";
    public string ReplyFormat { get; set; } = "";
    public string PromptVia { get; set; } = "";
    public string ReplyJsonField { get; set; } = "";
}
