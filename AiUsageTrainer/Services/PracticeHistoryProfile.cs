using System;
using System.Collections.Generic;
using System.Linq;
using AiUsageTrainer.Models;

namespace AiUsageTrainer.Services;

/// <summary>
/// What every saved report adds up to: which (domain, problem type) combinations have been
/// practiced, how recently, and how they went. Derived from the reports on each load rather
/// than stored separately, so deleting a report immediately corrects the picture.
/// </summary>
public sealed class PracticeHistoryProfile
{
    private readonly Dictionary<(string Domain, string TypeKey), DomainTypeStatus> _byKey = new();

    private PracticeHistoryProfile() { }

    public int ReportCount { get; private set; }

    public static PracticeHistoryProfile Build(IEnumerable<PracticeReport> reports)
    {
        var profile = new PracticeHistoryProfile();

        foreach (var report in reports.OrderBy(r => r.CreatedAt))
        {
            profile.ReportCount++;

            var domain = report.Config.PlannedDomain;
            var type = report.Config.PlannedTypeKey;
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(type)) continue;

            var key = (domain, type);
            if (!profile._byKey.TryGetValue(key, out var status))
            {
                status = new DomainTypeStatus { Domain = domain, TypeKey = type };
                profile._byKey[key] = status;
            }

            status.TimesPracticed++;
            status.LastPracticedAt = report.CreatedAt;
            status.LastGoodCriteria = report.Evaluation.GoodCriteriaCount;
            status.LastCriteriaTotal = report.Evaluation.Criteria.Count;
        }

        return profile;
    }

    public DomainTypeStatus StatusOf(string domain, string typeKey)
        => _byKey.TryGetValue((domain, typeKey), out var s)
            ? s
            : new DomainTypeStatus { Domain = domain, TypeKey = typeKey };

    /// <summary>Every (domain, type) combination for the given domains/types, practiced or not.</summary>
    public List<DomainTypeStatus> StatusesFor(IEnumerable<string> domains, IEnumerable<string> typeKeys)
    {
        var list = new List<DomainTypeStatus>();
        foreach (var d in domains)
            foreach (var t in typeKeys)
                list.Add(StatusOf(d, t));
        return list;
    }

    /// <summary>Problem titles already used for a combination, so generation can avoid repeats.</summary>
    public List<string> TitlesFor(IEnumerable<PracticeReport> reports, string domain, string typeKey)
        => reports
            .Where(r => r.Config.PlannedDomain == domain && r.Config.PlannedTypeKey == typeKey)
            .Select(r => r.Problem.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();
}
