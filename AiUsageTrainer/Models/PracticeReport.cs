using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace AiUsageTrainer.Models;

/// <summary>미흡 / 부분적 / 충분 중 하나. 문자열로 느슨하게 받아 모델이 다른 표현을 써도 화면이 깨지지 않는다.</summary>
public sealed class ProcessStepEval
{
    [JsonPropertyName("step")]
    public string Step { get; set; } = "";

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "";

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = "";

    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = "";

    [JsonIgnore]
    public bool IsGood => Rating.Contains("충분");

    [JsonIgnore]
    public bool IsPartial => Rating.Contains("부분");
}

public sealed class CriterionEval
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = "";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("tip")]
    public string Tip { get; set; } = "";

    [JsonIgnore]
    public bool IsGood => Rating.Contains("충분");

    [JsonIgnore]
    public bool IsPartial => Rating.Contains("부분");

    [JsonIgnore]
    public bool HasTip => !string.IsNullOrWhiteSpace(Tip);
}

/// <summary>The part of the report the model produces.</summary>
public sealed class Evaluation
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("problem_recap")]
    public string ProblemRecap { get; set; } = "";

    [JsonPropertyName("overall_comment")]
    public string OverallComment { get; set; } = "";

    /// <summary>6단계 프롬프트 패턴: 문제 재정의/계획/학습/작은 단위 요청/검증/마무리.</summary>
    [JsonPropertyName("process_steps")]
    public List<ProcessStepEval> ProcessSteps { get; set; } = new();

    /// <summary>5개 평가 항목: 문제정의/학습흡수/결과검증/커뮤니케이션/전공지식.</summary>
    [JsonPropertyName("criteria")]
    public List<CriterionEval> Criteria { get; set; } = new();

    [JsonPropertyName("banned_phrases_used")]
    public List<string> BannedPhrasesUsed { get; set; } = new();

    [JsonPropertyName("good_prompts")]
    public List<string> GoodPrompts { get; set; } = new();

    [JsonPropertyName("deliverable_check")]
    public string DeliverableCheck { get; set; } = "";

    [JsonPropertyName("final_advice")]
    public string FinalAdvice { get; set; } = "";

    [JsonIgnore]
    public int GoodCriteriaCount => Criteria.Count(c => c.IsGood);
}

/// <summary>A saved practice session: the problem, the evaluation, and everything to reopen it.</summary>
public sealed class PracticeReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public PracticeConfig Config { get; set; } = new();
    public ProblemBrief Problem { get; set; } = new();
    public Evaluation Evaluation { get; set; } = new();
    public List<ChatMessage> Transcript { get; set; } = new();
    public string WorkspacePath { get; set; } = "";
    public List<string> WorkspaceFiles { get; set; } = new();
    public int DurationSeconds { get; set; }
    public bool EndedEarly { get; set; }

    /// <summary>Raw model text, kept so a parse failure never loses the evaluation.</summary>
    public string RawEvaluation { get; set; } = "";

    [JsonIgnore]
    public string Title => string.IsNullOrWhiteSpace(Problem.Title) ? "제목 없는 연습" : Problem.Title;

    [JsonIgnore]
    public string DateLabel => CreatedAt.ToString("yyyy-MM-dd HH:mm");

    [JsonIgnore]
    public string DomainTypeLabel
    {
        get
        {
            var type = ProblemTypeCatalog.ByKey(Config.PlannedTypeKey)?.Name ?? Config.PlannedTypeKey;
            return string.IsNullOrWhiteSpace(Config.PlannedDomain) ? type : $"{Config.PlannedDomain} · {type}";
        }
    }

    [JsonIgnore]
    public string DurationLabel => DurationSeconds >= 3600
        ? $"{DurationSeconds / 3600}시간 {(DurationSeconds % 3600) / 60}분"
        : $"{DurationSeconds / 60}분 {DurationSeconds % 60}초";

    [JsonIgnore]
    public string GoodCriteriaLabel => $"충분 {Evaluation.GoodCriteriaCount} / {Evaluation.Criteria.Count}";
}
