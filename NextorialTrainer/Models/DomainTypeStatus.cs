using System;

namespace NextorialTrainer.Models;

/// <summary>How much a (domain, problem type) combination has been practiced, derived from saved reports.</summary>
public sealed class DomainTypeStatus
{
    public required string Domain { get; init; }
    public required string TypeKey { get; init; }

    public int TimesPracticed { get; set; }
    public DateTime? LastPracticedAt { get; set; }
    public int LastGoodCriteria { get; set; }
    public int LastCriteriaTotal { get; set; }

    public string TypeName => ProblemTypeCatalog.ByKey(TypeKey)?.Name ?? TypeKey;

    public int DaysSincePracticed => LastPracticedAt is null
        ? int.MaxValue
        : Math.Max(0, (int)(DateTime.Now - LastPracticedAt.Value).TotalDays);

    public string HistoryLabel => TimesPracticed == 0
        ? "아직 안 풀어봄"
        : $"{TimesPracticed}회 · 최근 {LastPracticedAt:yyyy-MM-dd} · 충분 {LastGoodCriteria}/{LastCriteriaTotal}";
}

/// <summary>The domain/type combination the planner picked for the upcoming session.</summary>
public sealed class PlannedProblem
{
    public string Domain { get; set; } = "";
    public string TypeKey { get; set; } = "";
    public string StateLabel { get; set; } = "";

    public string TypeName => ProblemTypeCatalog.ByKey(TypeKey)?.Name ?? TypeKey;
    public string DisplayLine => $"{Domain} · {TypeName}";
}
