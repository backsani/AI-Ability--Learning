using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NextorialTrainer.Common;
using NextorialTrainer.Models;

namespace NextorialTrainer.Services;

/// <summary>
/// Owns one practice session: generating the problem, the collaborator conversation, and the
/// closing evaluation. Knows nothing about which CLI is behind it, nor about the UI.
/// </summary>
public sealed class PracticeSession
{
    private readonly CliAgentBackend _backend;
    private ICliConversation? _collab;
    private string _workspacePath = "";

    public PracticeSession(CliAgentBackend backend, PracticeConfig config)
    {
        _backend = backend;
        Config = config;
    }

    public PracticeConfig Config { get; }
    public ProblemBrief Problem { get; private set; } = new();
    public string WorkspacePath => _workspacePath;
    public double CostUsd => _backend.TotalCostUsd;

    public async Task<ProblemBrief> GenerateProblemAsync(CancellationToken ct)
    {
        _workspacePath = WorkspaceManager.CreateNew();

        var conversation = _backend.CreateEvalConversation("너는 JSON만 출력하는 문제 출제 도우미다.");
        var prompt = PromptBuilder.BuildProblemGenerationPrompt(Config, Config.AvoidTitles);
        var text = await conversation.SendAsync(prompt, Config.EvalModel, ct).ConfigureAwait(false);

        var brief = ParseProblem(text);

        foreach (var f in brief.StarterFiles)
            WorkspaceManager.WriteStarterFile(_workspacePath, f.Path, f.Content);

        Problem = brief;
        return brief;
    }

    private static ProblemBrief ParseProblem(string text)
    {
        var json = JsonBlock.Extract(text);
        if (json is not null)
        {
            try
            {
                var brief = JsonSerializer.Deserialize<ProblemBrief>(json);
                if (brief is not null && !string.IsNullOrWhiteSpace(brief.Statement)) return brief;
            }
            catch (JsonException) { /* fall through */ }
        }

        return new ProblemBrief
        {
            Title = "문제 생성 결과를 해석하지 못했습니다",
            Statement = "문제를 구조화된 형식으로 받지 못했습니다. 아래는 모델의 원문 응답입니다:\n\n" + text.Trim()
        };
    }

    public async Task<string> SendAsync(string userMessage, CancellationToken ct)
    {
        _collab ??= _backend.CreateCollabConversation(
            PromptBuilder.BuildCollabSystemPrompt(Config, Problem), _workspacePath);

        return await _collab.SendAsync(userMessage, Config.CollabModel, ct).ConfigureAwait(false);
    }

    /// <summary>Closing turn. Runs on the eval model with tools disabled, seeing the full transcript.</summary>
    public async Task<(Evaluation Evaluation, string Raw)> EvaluateAsync(
        IReadOnlyList<ChatMessage> transcript, string durationLabel, bool endedEarly, CancellationToken ct)
    {
        var conversation = _backend.CreateEvalConversation("너는 JSON만 출력하는 평가 도우미다.");
        var snapshot = WorkspaceManager.BuildSnapshot(_workspacePath);
        var prompt = PromptBuilder.BuildEvaluationPrompt(
            Config, Problem, PromptBuilder.RenderTranscript(transcript), snapshot, durationLabel, endedEarly);

        var text = await conversation.SendAsync(prompt, Config.EvalModel, ct).ConfigureAwait(false);

        var json = JsonBlock.Extract(text);
        if (json is null) return (FallbackEvaluation(text), text);

        try
        {
            var evaluation = JsonSerializer.Deserialize<Evaluation>(json);
            if (evaluation is null || string.IsNullOrWhiteSpace(evaluation.OverallComment))
                return (FallbackEvaluation(text), text);

            if (string.IsNullOrWhiteSpace(evaluation.Title)) evaluation.Title = Problem.Title;
            return (evaluation, text);
        }
        catch (JsonException)
        {
            return (FallbackEvaluation(text), text);
        }
    }

    private Evaluation FallbackEvaluation(string raw) => new()
    {
        Title = Problem.Title,
        ProblemRecap = Problem.Title,
        OverallComment = "평가 결과를 구조화하지 못했습니다. 아래 원문을 확인해 주세요.",
        DeliverableCheck = raw.Trim()
    };
}
