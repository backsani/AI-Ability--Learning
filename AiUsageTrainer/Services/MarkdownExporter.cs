using System.Collections.Generic;
using System.Text;
using AiUsageTrainer.Models;

namespace AiUsageTrainer.Services;

public static class MarkdownExporter
{
    public static string ToMarkdown(PracticeReport r, bool includeTranscript)
    {
        var e = r.Evaluation;
        var sb = new StringBuilder();

        sb.AppendLine($"# {r.Title}");
        sb.AppendLine();
        sb.AppendLine($"- 일시: {r.DateLabel}");
        sb.AppendLine($"- 분야·유형: {r.DomainTypeLabel}");
        sb.AppendLine($"- 난이도 기준: {r.Config.Difficulty}");
        sb.AppendLine($"- 소요 시간: {r.DurationLabel}");
        if (r.EndedEarly) sb.AppendLine("- 중도 제출된 세션입니다.");
        sb.AppendLine();

        sb.AppendLine($"> {e.ProblemRecap}");
        sb.AppendLine();
        sb.AppendLine("## 총평");
        sb.AppendLine(e.OverallComment);
        sb.AppendLine();

        sb.AppendLine("## 프롬프트 6단계 점검");
        sb.AppendLine();
        foreach (var s in e.ProcessSteps)
        {
            sb.AppendLine($"### {s.Step} — {s.Rating}");
            sb.AppendLine($"- 근거: {s.Evidence}");
            sb.AppendLine($"- 제안: {s.Suggestion}");
            sb.AppendLine();
        }

        sb.AppendLine("## 5개 평가 항목");
        sb.AppendLine();
        foreach (var c in e.Criteria)
        {
            sb.AppendLine($"### {c.Name} — {c.Rating}");
            sb.AppendLine(c.Comment);
            if (!string.IsNullOrWhiteSpace(c.Tip)) sb.AppendLine($"- 다음에 시도해볼 것: {c.Tip}");
            sb.AppendLine();
        }

        if (e.BannedPhrasesUsed.Count > 0)
        {
            sb.AppendLine("## 막연하게 위임한 표현");
            AppendList(sb, e.BannedPhrasesUsed);
        }

        if (e.GoodPrompts.Count > 0)
        {
            sb.AppendLine("## 잘 쓴 프롬프트");
            AppendList(sb, e.GoodPrompts);
        }

        if (!string.IsNullOrWhiteSpace(e.DeliverableCheck))
        {
            sb.AppendLine("## 결과물 검토");
            sb.AppendLine(e.DeliverableCheck);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(e.FinalAdvice))
        {
            sb.AppendLine("## 다음에 시도해볼 것");
            sb.AppendLine(e.FinalAdvice);
            sb.AppendLine();
        }

        if (r.Problem.HiddenAcceptanceCriteria.Count > 0)
        {
            sb.AppendLine("## 이 문제가 실제로 확인하려던 것");
            AppendList(sb, r.Problem.HiddenAcceptanceCriteria);
        }

        if (includeTranscript && r.Transcript.Count > 0)
        {
            sb.AppendLine("## 대화 전문");
            sb.AppendLine();
            foreach (var m in r.Transcript)
            {
                if (m.Role == ChatRole.System) continue;
                sb.AppendLine($"**{m.RoleLabel}** ({m.TimeLabel})");
                sb.AppendLine();
                sb.AppendLine(m.Text);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void AppendList(StringBuilder sb, List<string> items)
    {
        foreach (var i in items) sb.AppendLine($"- {i}");
        sb.AppendLine();
    }
}
