using System;
using System.Collections.Generic;
using System.Linq;
using NextorialTrainer.Models;

namespace NextorialTrainer.Services;

/// <summary>
/// Picks which (domain, problem type) combination the next session covers. Done by the app
/// rather than left to the model so repeated sessions actually spread across combinations
/// instead of drifting back to whatever the model finds easiest to generate.
/// </summary>
public static class ProblemPlanner
{
    private const int UnexploredWeight = 100;
    private const int RecentLowScoreWeight = 75;
    private const int PracticedWeight = 35;

    /// <summary>Older combinations drift back up the list, up to this much.</summary>
    private const int MaxStalenessBonus = 25;

    private const int Jitter = 15;

    public static PlannedProblem Pick(
        IReadOnlyList<string> domains, IReadOnlyList<string> typeKeys,
        PracticeHistoryProfile profile, int? seed = null)
    {
        var random = seed is null ? new Random() : new Random(seed.Value);

        var candidates = profile.StatusesFor(domains, typeKeys);
        if (candidates.Count == 0)
        {
            return new PlannedProblem
            {
                Domain = domains.FirstOrDefault() ?? "일반",
                TypeKey = typeKeys.FirstOrDefault() ?? ProblemTypeCatalog.All[0].Key,
                StateLabel = "선택된 분야·유형이 없어 기본값을 사용합니다."
            };
        }

        var best = candidates
            .OrderByDescending(s => Score(s, random))
            .First();

        return new PlannedProblem
        {
            Domain = best.Domain,
            TypeKey = best.TypeKey,
            StateLabel = best.TimesPracticed == 0 ? "아직 안 풀어본 조합" : best.HistoryLabel
        };
    }

    private static int Score(DomainTypeStatus s, Random random)
    {
        int baseWeight;
        if (s.TimesPracticed == 0)
        {
            baseWeight = UnexploredWeight;
        }
        else if (s.LastCriteriaTotal > 0 && s.LastGoodCriteria * 2 < s.LastCriteriaTotal)
        {
            baseWeight = RecentLowScoreWeight;
        }
        else
        {
            baseWeight = PracticedWeight;
        }

        var staleness = s.LastPracticedAt is null
            ? 0
            : Math.Min(MaxStalenessBonus, s.DaysSincePracticed * MaxStalenessBonus / 30);

        return baseWeight + staleness + random.Next(Jitter);
    }
}
