using System.Collections.Generic;
using System.Linq;
using AiUsageTrainer.Models;

namespace AiUsageTrainer.Services;

/// <summary>
/// Built-in starting points for each agent CLI.
///
/// Only the Claude Code profile has been exercised end to end by this app, including the
/// collaborator (tools-enabled) path: a live run confirmed --permission-mode bypassPermissions
/// lets it read/write/execute files in the session workspace without prompting, and
/// --system-prompt-snapshot on keeps the collaborator persona across --resume turns without
/// resending the system prompt every time.
///
/// The others are best-effort presets for the eval (no-tools) path only: agent CLIs change
/// their flags often, so every field here is editable in the settings screen, and the
/// connection test shows the exact command line and raw output so a wrong preset can be
/// corrected without touching the code. Their CollabFirstArgs is left empty, which falls back
/// to the eval template — meaning they will NOT actually manipulate files as a collaborator
/// unless you fill in a working preset for that CLI yourself.
/// </summary>
public static class CliAgentCatalog
{
    public const string ClaudeId = "claude";
    public const string CustomId = "custom";

    public static IReadOnlyList<CliAgentProfile> Profiles { get; } = new List<CliAgentProfile>
    {
        new()
        {
            Id = ClaudeId,
            DisplayName = "Claude Code",
            Description = "검증됨. 문제 출제·평가는 도구 없이, 협업(문제 풀이) 대화는 세션 작업 폴더 안에서 " +
                          "파일 읽기·쓰기·명령 실행이 가능한 상태로 동작합니다.",
            Verified = true,
            CollabVerified = true,
            ExecutableNames = { "claude.exe", "claude.cmd", "claude" },
            ExecutableFallbacks =
            {
                @"%USERPROFILE%\.local\bin\claude.exe",
                @"%USERPROFILE%\.claude\local\claude.exe",
                @"%APPDATA%\npm\claude.cmd"
            },
            SessionMode = SessionMode.Resume,
            PromptVia = PromptVia.Stdin,
            ReplyFormat = ReplyFormat.Json,
            ReplyJsonField = "result",
            ErrorJsonField = "is_error",
            CostJsonField = "total_cost_usd",
            SupportsSystemPromptFlag = true,

            // Eval turns (problem generation / evaluation): no tools, one shot, fixed empty cwd.
            // --safe-mode disables CLAUDE.md/skills/plugins/hooks/MCP for this process only (auth,
            // model selection, tools and permissions are unaffected) — confirmed live: a CLAUDE.md
            // instruction to prefix every reply was obeyed without --safe-mode and ignored with it.
            // Without this, a stray CLAUDE.md anywhere from the cwd up to the user's home directory
            // could leak unrelated persona/context into the generated problem or the evaluation.
            FirstArgs = "-p --output-format json --session-id {sessionId} [--system-prompt {systemPrompt}] " +
                        "[--model {model}] --tools \"\" --permission-prompts none --safe-mode --strict-mcp-config",
            ResumeArgs = "-p --output-format json --resume {sessionId} [--system-prompt {systemPrompt}] " +
                         "[--model {model}] --tools \"\" --permission-prompts none --safe-mode --strict-mcp-config",

            // Collab turns (the practice conversation): tools on, permissions auto-approved,
            // confined to the session workspace folder (the process working directory).
            // --system-prompt-snapshot on means the persona survives --resume without resending
            // --system-prompt every turn (confirmed live: a resume turn with no --system-prompt
            // still answered in character). --safe-mode here matters even more than for eval turns,
            // since the collaborator has tools — an injected CLAUDE.md could otherwise change how
            // it behaves (its own hooks, MCP servers, custom commands) during the user's practice.
            CollabFirstArgs = "-p --output-format json --session-id {sessionId} [--system-prompt {systemPrompt}] " +
                               "--system-prompt-snapshot on [--model {model}] --permission-mode bypassPermissions " +
                               "--safe-mode --strict-mcp-config",
            CollabResumeArgs = "-p --output-format json --resume {sessionId} [--model {model}] " +
                                "--permission-mode bypassPermissions --safe-mode --strict-mcp-config",

            Models = { "sonnet", "opus", "haiku" },
            DefaultCollabModel = "sonnet",
            DefaultEvalModel = "sonnet"
        },

        new()
        {
            Id = "codex",
            DisplayName = "OpenAI Codex CLI",
            Description = "미검증 프리셋. 협업 대화의 실제 파일 조작은 확인되지 않았습니다. " +
                          "동작하지 않으면 아래 [고급]에서 인자를 수정하세요.",
            ExecutableNames = { "codex.exe", "codex.cmd", "codex" },
            ExecutableFallbacks = { @"%APPDATA%\npm\codex.cmd" },
            SessionMode = SessionMode.Replay,
            PromptVia = PromptVia.Arg,
            ReplyFormat = ReplyFormat.Raw,
            SupportsSystemPromptFlag = false,
            FirstArgs = "exec --skip-git-repo-check [--model {model}] [{prompt}]",
            Models = { "gpt-5-codex", "gpt-5" },
            DefaultCollabModel = "",
            DefaultEvalModel = ""
        },

        new()
        {
            Id = "gemini",
            DisplayName = "Gemini CLI",
            Description = "미검증 프리셋. 협업 대화의 실제 파일 조작은 확인되지 않았습니다. " +
                          "동작하지 않으면 아래 [고급]에서 인자를 수정하세요.",
            ExecutableNames = { "gemini.exe", "gemini.cmd", "gemini" },
            ExecutableFallbacks = { @"%APPDATA%\npm\gemini.cmd" },
            SessionMode = SessionMode.Replay,
            PromptVia = PromptVia.Arg,
            ReplyFormat = ReplyFormat.Raw,
            SupportsSystemPromptFlag = false,
            FirstArgs = "[--model {model}] [--prompt {prompt}]",
            Models = { "gemini-2.5-pro", "gemini-2.5-flash" },
            DefaultCollabModel = "",
            DefaultEvalModel = ""
        },

        new()
        {
            Id = "cursor-agent",
            DisplayName = "Cursor Agent CLI",
            Description = "미검증 프리셋. 협업 대화의 실제 파일 조작은 확인되지 않았습니다. " +
                          "동작하지 않으면 아래 [고급]에서 인자를 수정하세요.",
            ExecutableNames = { "cursor-agent.exe", "cursor-agent.cmd", "cursor-agent" },
            SessionMode = SessionMode.Replay,
            PromptVia = PromptVia.Arg,
            ReplyFormat = ReplyFormat.Raw,
            SupportsSystemPromptFlag = false,
            FirstArgs = "--print --output-format text [--model {model}] [{prompt}]",
            Models = { "sonnet-4.5", "gpt-5" },
            DefaultCollabModel = "",
            DefaultEvalModel = ""
        },

        new()
        {
            Id = "opencode",
            DisplayName = "opencode",
            Description = "미검증 프리셋. 협업 대화의 실제 파일 조작은 확인되지 않았습니다. " +
                          "동작하지 않으면 아래 [고급]에서 인자를 수정하세요.",
            ExecutableNames = { "opencode.exe", "opencode.cmd", "opencode" },
            SessionMode = SessionMode.Replay,
            PromptVia = PromptVia.Arg,
            ReplyFormat = ReplyFormat.Raw,
            SupportsSystemPromptFlag = false,
            FirstArgs = "run [--model {model}] [{prompt}]",
            Models = { },
            DefaultCollabModel = "",
            DefaultEvalModel = ""
        },

        new()
        {
            Id = CustomId,
            DisplayName = "직접 설정 (범용)",
            Description = "프롬프트를 받아 답변을 출력하는 CLI라면 무엇이든. 경로와 인자를 직접 지정하세요.",
            SessionMode = SessionMode.Replay,
            PromptVia = PromptVia.Stdin,
            ReplyFormat = ReplyFormat.Raw,
            SupportsSystemPromptFlag = false,
            FirstArgs = "",
            Models = { },
            DefaultCollabModel = "",
            DefaultEvalModel = ""
        }
    };

    public static CliAgentProfile Find(string? id)
        => Profiles.FirstOrDefault(p => p.Id == id) ?? Profiles[0];

    public static IReadOnlyList<string> Ids => Profiles.Select(p => p.Id).ToList();
}
