using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NextorialTrainer.Common;
using NextorialTrainer.Models;

namespace NextorialTrainer.Services;

public sealed class AgentCliException : Exception
{
    public AgentCliException(string message, string? detail = null, string? commandLine = null)
        : base(message)
    {
        Detail = detail;
        CommandLine = commandLine;
    }

    public string? Detail { get; }
    public string? CommandLine { get; }
}

/// <summary>One CLI conversation, however that agent keeps its state.</summary>
public interface ICliConversation
{
    Task<string> SendAsync(string userMessage, string model, CancellationToken ct);
    double TotalCostUsd { get; }
}

/// <summary>
/// Drives any agent CLI described by a <see cref="CliAgentProfile"/>: builds the argument list
/// from the profile's template, feeds the prompt in, and pulls the reply back out.
/// </summary>
public sealed class CliAgentBackend
{
    private readonly Func<CliAgentProfile> _profileProvider;
    private readonly Func<string> _pathProvider;

    public CliAgentBackend(Func<CliAgentProfile> profileProvider, Func<string> pathProvider)
    {
        _profileProvider = profileProvider;
        _pathProvider = pathProvider;
    }

    public CliAgentProfile Profile => _profileProvider();

    public double TotalCostUsd { get; private set; }

    /// <summary>Command line of the most recent turn, shown by the connection test.</summary>
    public string LastCommandLine { get; private set; } = "";

    /// <summary>Looks for the executable on PATH and in the profile's known install locations.</summary>
    public static string? DetectPath(CliAgentProfile profile)
    {
        foreach (var raw in profile.ExecutableFallbacks)
        {
            var expanded = Environment.ExpandEnvironmentVariables(raw);
            if (SafeExists(expanded)) return expanded;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in profile.ExecutableNames)
            {
                string candidate;
                try { candidate = Path.Combine(dir.Trim(), name); }
                catch (ArgumentException) { continue; }

                if (SafeExists(candidate)) return candidate;
            }
        }

        return null;
    }

    public string ResolvePath()
    {
        var profile = Profile;
        var configured = _pathProvider();
        if (!string.IsNullOrWhiteSpace(configured) && SafeExists(configured)) return configured;

        var detected = DetectPath(profile);
        if (detected is not null) return detected;

        throw new AgentCliException(
            $"{profile.DisplayName} 실행 파일을 찾지 못했습니다. 설정 화면에서 경로를 지정해 주세요.");
    }

    /// <summary>
    /// A no-tools, one-shot conversation used for problem generation and evaluation. Always
    /// runs in the empty scratch folder so it never picks up a stray CLAUDE.md.
    /// </summary>
    public ICliConversation CreateEvalConversation(string systemPrompt)
        => new CliConversation(this, Profile, systemPrompt, collab: false, AppPaths.CliWorkDir);

    /// <summary>
    /// The practice conversation: tools enabled, confined to the given session workspace so the
    /// agent can actually read/write/run code there.
    /// </summary>
    public ICliConversation CreateCollabConversation(string systemPrompt, string workingDirectory)
        => new CliConversation(this, Profile, systemPrompt, collab: true, workingDirectory);

    /// <summary>Runs a single turn. Returns the reply plus the raw output for diagnostics.</summary>
    internal async Task<(string Reply, string RawStdout, string CommandLine)> RunAsync(
        List<string> args, string? stdinText, string workingDirectory, CancellationToken ct)
    {
        AppPaths.EnsureCreated();
        Directory.CreateDirectory(workingDirectory);
        var exe = ResolvePath();
        var profile = Profile;
        var commandLine = Describe(exe, args);
        LastCommandLine = commandLine;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Pin stdin too: left unset it follows the host console's code page (CP949 on a
            // Korean Windows), which turns every Korean prompt into mojibake for the CLI.
            StandardInputEncoding = Utf8.NoBom,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };

        try { process.Start(); }
        catch (Exception ex)
        {
            throw new AgentCliException($"실행에 실패했습니다: {ex.Message}", exe, commandLine);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await using (var stdin = process.StandardInput)
            {
                if (!string.IsNullOrEmpty(stdinText))
                    await stdin.WriteAsync(stdinText.AsMemory(), ct).ConfigureAwait(false);
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
            throw new AgentCliException(
                $"{profile.DisplayName}이(가) 오류 코드 {process.ExitCode}로 종료되었습니다.",
                string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
                commandLine);

        var reply = ExtractReply(profile, stdout, stderr, commandLine);
        return (reply, stdout, commandLine);
    }

    private string ExtractReply(CliAgentProfile profile, string stdout, string stderr, string commandLine)
    {
        if (profile.ReplyFormat == ReplyFormat.Raw)
        {
            var raw = stdout.Trim();
            if (raw.Length == 0)
                throw new AgentCliException(
                    $"{profile.DisplayName}이(가) 아무것도 출력하지 않았습니다.",
                    string.IsNullOrWhiteSpace(stderr) ? "(stderr 없음)" : stderr,
                    commandLine);
            return raw;
        }

        var json = JsonBlock.Extract(stdout);
        if (json is null)
            throw new AgentCliException(
                "응답을 JSON으로 해석하지 못했습니다. [설정]의 [고급]에서 응답 형식을 '원문'으로 바꿔보세요.",
                string.IsNullOrWhiteSpace(stdout) ? stderr : stdout,
                commandLine);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!string.IsNullOrEmpty(profile.ErrorJsonField) &&
            root.TryGetProperty(profile.ErrorJsonField, out var err) &&
            err.ValueKind == JsonValueKind.True)
        {
            var msg = root.TryGetProperty(profile.ReplyJsonField, out var errText) ? errText.GetString() : null;
            throw new AgentCliException($"{profile.DisplayName}이(가) 오류를 반환했습니다.",
                msg ?? stdout, commandLine);
        }

        if (!string.IsNullOrEmpty(profile.CostJsonField) &&
            root.TryGetProperty(profile.CostJsonField, out var cost) &&
            cost.ValueKind == JsonValueKind.Number)
        {
            TotalCostUsd += cost.GetDouble();
        }

        if (!root.TryGetProperty(profile.ReplyJsonField, out var replyElement))
            throw new AgentCliException(
                $"응답 JSON에 '{profile.ReplyJsonField}' 필드가 없습니다. [설정]의 [고급]에서 필드 이름을 확인하세요.",
                stdout, commandLine);

        return replyElement.GetString() ?? "";
    }

    private static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch (Exception) { return false; }
    }

    private static string Describe(string exe, List<string> args)
    {
        var sb = new StringBuilder(exe.Contains(' ') ? $"\"{exe}\"" : exe);
        foreach (var a in args)
        {
            sb.Append(' ');
            if (a.Length == 0) sb.Append("\"\"");
            else if (a.Contains(' ')) sb.Append('"').Append(Shorten(a)).Append('"');
            else sb.Append(Shorten(a));
        }
        return sb.ToString();
    }

    private static string Shorten(string s)
        => s.Length > 120 ? s.Substring(0, 120) + "…(생략)" : s;

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (Exception) { /* already gone */ }
    }

    /// <summary>
    /// A conversation over one CLI. In Resume mode the CLI keeps the history and we send only
    /// the new message; in Replay mode we keep the history and re-send the whole transcript,
    /// which is what lets an arbitrary prompt-in/text-out CLI act as the collaborator or
    /// evaluator. <paramref name="collab"/> picks the tools-enabled vs no-tools argument
    /// templates and, together with <paramref name="workingDirectory"/>, decides whether the
    /// agent can actually touch files.
    /// </summary>
    private sealed class CliConversation : ICliConversation
    {
        private readonly CliAgentBackend _backend;
        private readonly CliAgentProfile _profile;
        private readonly string _systemPrompt;
        private readonly bool _collab;
        private readonly string _workingDirectory;
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly List<(string Role, string Text)> _history = new();
        private bool _started;

        public CliConversation(CliAgentBackend backend, CliAgentProfile profile, string systemPrompt,
            bool collab, string workingDirectory)
        {
            _backend = backend;
            _profile = profile;
            _systemPrompt = systemPrompt;
            _collab = collab;
            _workingDirectory = workingDirectory;
        }

        public double TotalCostUsd => _backend.TotalCostUsd;

        public async Task<string> SendAsync(string userMessage, string model, CancellationToken ct)
        {
            var replay = _profile.SessionMode == SessionMode.Replay;
            var prompt = replay ? RenderTranscript(userMessage) : userMessage;

            if (string.IsNullOrWhiteSpace(prompt))
                throw new AgentCliException("보낼 내용이 비어 있어 요청하지 않았습니다.");

            var firstArgs = _collab && !string.IsNullOrWhiteSpace(_profile.CollabFirstArgs)
                ? _profile.CollabFirstArgs : _profile.FirstArgs;
            var resumeArgs = _collab && !string.IsNullOrWhiteSpace(_profile.CollabResumeArgs)
                ? _profile.CollabResumeArgs : _profile.ResumeArgs;

            var template = !replay && _started && !string.IsNullOrWhiteSpace(resumeArgs)
                ? resumeArgs
                : firstArgs;

            var viaArg = _profile.PromptVia == PromptVia.Arg;

            var values = new Dictionary<string, string>
            {
                ["sessionId"] = _sessionId,
                ["model"] = model ?? "",
                ["systemPrompt"] = _profile.SupportsSystemPromptFlag ? _systemPrompt : "",
                ["prompt"] = viaArg ? prompt : ""
            };

            var args = ArgTemplate.Build(template, values);

            // In Arg mode a template without a {prompt} placeholder would drop the prompt on the
            // floor: it is not in the arguments and stdin is deliberately left empty. Append it as
            // a trailing positional argument, which is what these CLIs expect anyway.
            if (viaArg && !template.Contains("{prompt}", StringComparison.Ordinal))
                args.Add(prompt);

            var stdin = viaArg ? null : prompt;

            var (reply, _, _) = await _backend.RunAsync(args, stdin, _workingDirectory, ct).ConfigureAwait(false);

            _started = true;
            _history.Add(("사용자", userMessage));
            _history.Add((_collab ? "협업 AI" : "AI", reply));

            return reply;
        }

        /// <summary>Rebuilds the whole conversation as a single prompt for stateless CLIs.</summary>
        private string RenderTranscript(string userMessage)
        {
            var sb = new StringBuilder();

            if (!_profile.SupportsSystemPromptFlag)
            {
                sb.AppendLine(_systemPrompt);
                sb.AppendLine();
                sb.AppendLine("--------");
                sb.AppendLine();
            }

            if (_history.Count > 0)
            {
                sb.AppendLine("# 지금까지의 대화 기록");
                sb.AppendLine();
                foreach (var (role, text) in _history)
                {
                    sb.AppendLine($"## {role}");
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
                sb.AppendLine("--------");
                sb.AppendLine();
            }

            sb.AppendLine("# 사용자의 새 메시지");
            sb.AppendLine(userMessage);
            sb.AppendLine();
            sb.AppendLine("위 지시와 기록을 따라 다음 응답을 하라.");

            return sb.ToString();
        }
    }
}
