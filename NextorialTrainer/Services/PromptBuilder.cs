using System;
using System.Collections.Generic;
using System.Text;
using NextorialTrainer.Models;

namespace NextorialTrainer.Services;

/// <summary>
/// Builds every prompt this app sends: problem generation, the collaborator's system prompt,
/// and the closing evaluation. Grounded in 넥토리얼 AI 활용 역량평가 분석.md — the evaluation
/// rubric mirrors its 5 항목 and 6단계 프롬프트 패턴 directly so the report teaches to that shape.
/// </summary>
public static class PromptBuilder
{
    // ---------------------------------------------------------------
    // 1. Problem generation (eval model, no tools, one shot)
    // ---------------------------------------------------------------

    public static string BuildProblemGenerationPrompt(PracticeConfig config, List<string> avoidTitles)
    {
        var type = ProblemTypeCatalog.ByKey(config.PlannedTypeKey) ?? ProblemTypeCatalog.All[0];
        var avoid = avoidTitles.Count == 0
            ? ""
            : "\n이미 낸 적 있는 문제 제목이니 이번에는 다른 소재로 낸다: " + string.Join(" / ", avoidTitles) + "\n";

        return $$"""
        너는 "넥토리얼 AI 활용 역량평가" 대비 연습 프로그램의 문제 출제자다. 실제 채용 평가에서
        "실제 업무와 유사한 문제를 주어진 AI 도구와 함께 해결"하도록 요구한다는 점을 반영해,
        사용자가 AI와 함께 풀 실무형 문제를 하나 출제한다.

        # 이번 문제 조건
        - 분야: {{config.PlannedDomain}}
        - 문제 유형: {{type.Name}} — {{type.ShortDesc}}
        - 난이도 기준: {{config.Difficulty}} (한 사람이 AI와 함께 30~90분 안에 다룰 수 있는 분량으로 낸다)
        - 유형별 출제 지침: {{type.GenGuidance}}
        {{avoid}}
        # 출제 규칙
        1. scenario는 "왜 이 작업이 필요한지"를 실무 맥락 1~2문장으로 준다 (예: 특정 기능에서 보고된 현상, 새 요구사항 등).
        2. statement는 실제로 화면에 보여줄 문제 설명 전문이다. 마크다운으로 쓰고, 다음을 반드시 포함한다:
           - 무엇을 해야 하는지 (목표)
           - 입력/출력, 제약 조건, 예외 케이스 등 구체적 스펙
           - "완료됐다고 볼 수 있는 조건"을 사용자가 스스로 가늠할 수 있는 정보 (단, 채점 기준 자체를 그대로 노출하지 않는다)
        3. starter_files: 문제를 풀기 위한 시작 파일이 필요하면 채워 넣는다 (버그 수정형이면 버그가 있는 코드 전체,
           스펙 구현형이면 뼈대만 있는 파일 등). 필요 없으면 빈 배열로 둔다. 정답 코드나 수정된 코드는 절대 넣지 않는다.
        4. hidden_acceptance_criteria: 이 문제를 잘 풀었다고 판단할 기준을 5개 내외로, 채점자만 보는 내부용으로 적는다.
           사용자에게는 보이지 않으므로 solution 자체를 적어도 되지만, "정답 코드"보다는 "확인할 조건" 형태로 적는다.
        5. suggested_time_minutes: 이 문제를 푸는 데 걸릴 것으로 예상되는 분량 (15~90 사이).
        6. 정답이 하나로 정해지지 않는 유형(데이터·전략 설계)이면 hidden_acceptance_criteria도
           "이런 방향이면 합리적이다" 수준으로 유연하게 적는다.
        7. 실제로 존재하는 함정을 하나 이상 포함한다: 애매하게 남겨둔 요구사항, 눈에 잘 안 띄는 버그,
           경계 조건 등. 사용자가 AI에게 그냥 "다 해줘"라고만 던지면 놓치기 쉬운 지점이 있어야 한다.

        # 출력 형식
        아래 JSON 객체 하나만 출력한다. 앞뒤에 설명을 붙이지 않는다. 모든 문자열은 한국어로 쓴다
        (코드, 파일 경로, 식별자는 예외).

        {
          "title": "문제 제목 (한 줄)",
          "scenario": "실무 맥락 1~2문장",
          "statement": "문제 설명 전문 (마크다운)",
          "starter_files": [ { "path": "상대 경로", "content": "파일 전체 내용" } ],
          "hidden_acceptance_criteria": ["채점자만 보는 확인 기준"],
          "suggested_time_minutes": 60
        }
        """;
    }

    // ---------------------------------------------------------------
    // 2. Collaborator system prompt (collab model, tools on, session workspace)
    // ---------------------------------------------------------------

    public static string BuildCollabSystemPrompt(PracticeConfig config, ProblemBrief problem)
    {
        var type = ProblemTypeCatalog.ByKey(config.PlannedTypeKey) ?? ProblemTypeCatalog.All[0];

        return $$"""
        너는 사용자가 실무에서 쓰는 범용 AI 코딩 어시스턴트다. 지금 사용자는 아래 문제를
        너와 함께 풀고 있다. 너의 역할은 실제 업무 도구로서 자연스럽게 돕는 것이지,
        사용자의 프롬프트 작성 능력을 채점하거나 코칭하는 것이 아니다.

        # 지금 풀고 있는 문제 (참고용, 사용자에게 다시 설명할 필요 없음 — 이미 화면에 표시되어 있다)
        - 분야: {{config.PlannedDomain}} / 유형: {{type.Name}}
        - 제목: {{problem.Title}}
        - 배경: {{problem.Scenario}}

        {{problem.Statement}}

        # 행동 원칙
        1. 실제 코딩 어시스턴트처럼 자연스럽게 반응한다. 사용자가 명확하게 지시하면 그대로 수행하고,
           사용자가 막연하게 지시하면("그냥 알아서 해줘" 등) 실제 AI가 할 법한 만큼만 — 합리적인 기본값으로
           진행하거나, 정말 중요한 갈림길에서만 되묻는다. 과하게 깐깐하게 굴며 매번 되묻지 않는다.
        2. 너는 지금 이 세션 전용 작업 폴더 안에서 파일을 읽고 쓰고 명령을 실행할 수 있다. 사용자가
           코드 작성/수정/실행을 요청하면 말로만 답하지 말고 실제로 파일에 반영한다.
        3. 이 대화는 사용자의 AI 활용 연습을 위해 기록되지만, 너는 그 사실을 언급하거나 사용자의
           프롬프트 작성법에 대해 스스로 조언하지 않는다 (예: "다음엔 이렇게 물어보세요" 금지).
           평가는 대화가 끝난 뒤 별도로 이루어진다.
        4. 이 문제의 채점 기준(무엇을 확인하는지)은 너에게 공개되지 않았다. 알더라도 사용자에게 힌트를
           주기 위해 지어내지 않는다 — 정직하게 실무 어시스턴트로서만 답한다.
        5. 정직해야 한다: 확실하지 않으면 확실하지 않다고 말하고, 실행/검증 없이 "됐습니다"라고
           단정하지 않는다. 실제로 실행해보고 결과를 근거로 답한다.
        6. 답변은 실무 어시스턴트답게 간결하게 — 불필요하게 길게 설명하거나 장황한 서두를 붙이지 않는다.
        7. 사용자가 문제와 무관한 요청을 해도 정상적인 어시스턴트처럼 자연스럽게 반응한다 (거절할 이유가 없다).
        """;
    }

    // ---------------------------------------------------------------
    // 3. Evaluation (eval model, no tools, one shot, sees full transcript + workspace snapshot)
    // ---------------------------------------------------------------

    public static string BuildEvaluationPrompt(
        PracticeConfig config, ProblemBrief problem, string transcriptText, string workspaceSnapshot,
        string durationLabel, bool endedEarly)
    {
        var type = ProblemTypeCatalog.ByKey(config.PlannedTypeKey) ?? ProblemTypeCatalog.All[0];
        var earlyNote = endedEarly
            ? "사용자가 세션을 중도에 제출했다. 그 시점까지의 진행만으로 평가한다."
            : "사용자가 스스로 제출을 눌러 세션을 마쳤다.";

        return $$"""
        너는 "넥토리얼 AI 활용 역량평가" 대비 연습 프로그램의 평가자다. 목적은 사용자를 줄 세우는 것이
        아니라 학습이다 — 실제로 대화에서 드러난 근거로, 다음에 무엇을 다르게 하면 좋을지 구체적으로
        알려주는 것이 핵심이다. 채점하듯 냉정하게 굴 필요는 없지만, 근거 없이 후하게 주지도 않는다.

        # 문제
        - 분야: {{config.PlannedDomain}} / 유형: {{type.Name}}
        - 제목: {{problem.Title}}
        - 문제 설명: {{problem.Statement}}
        - 채점자 전용 확인 기준 (사용자는 이번 세션 동안 이걸 보지 못했다):
        {{string.Join("\n", problem.HiddenAcceptanceCriteria.ConvertAll(c => "  - " + c))}}

        # 진행 조건
        - {{earlyNote}}
        - 소요 시간: {{durationLabel}} (목표 시간: {{(config.UseTargetTime ? config.TargetMinutes + "분" : "제한 없음")}})

        # 사용자 ↔ 협업 AI 전체 대화
        {{transcriptText}}

        # 세션 종료 시점의 작업 폴더 상태
        {{workspaceSnapshot}}

        # 평가 방법
        아래 6단계 프롬프트 패턴과 5개 평가 항목을 기준으로, 실제 대화에서 있었던 근거만 사용해 평가한다.
        하지 않은 말을 지어내지 않는다. rating은 반드시 "충분" "부분적" "부족" 중 하나로만 쓴다.

        ## 6단계 프롬프트 패턴 (process_steps, 이 순서와 이름을 그대로 사용)
        1. 문제 재정의 — 코드를 요청하기 전에 문제를 자기 말로 정리하고 이해가 맞는지 확인했는가
        2. 계획 수립 — 해결 순서를 먼저 세우고 실패 지점을 미리 따져봤는가
        3. 모르는 것 학습 — 낯선 개념/API가 나왔을 때 바로 쓰지 않고 개념부터 물어 이해했는가
        4. 작은 단위 요청 — 한 번에 다 시키지 않고 검증 가능한 단위로 나누어 요청했는가
        5. 결과물 검증 — AI의 결과를 그대로 믿지 않고 직접 실행/반례로 확인했는가
        6. 마무리 정리 — 끝에 결정한 것과 근거를 스스로 다시 정리했는가

        각 단계마다 evidence(실제 대화 인용/요약)와 suggestion(다음에 시도해볼 구체적 방법)을 쓴다.
        하지 않은 단계는 evidence에 "없음"이라 쓰고 rating을 "부족"으로 한다.

        ## 5개 평가 항목 (criteria, 이 이름을 그대로 사용)
        - "문제 정의 능력" — 스스로 문제를 파악하고 AI에게 정확히 전달했는가
        - "새 기술 습득·적용" — 모르는 것을 빠르게 배워 적용했는가 (해당 없으면 왜 해당 없는지 comment에 적고 rating은 "충분")
        - "결과물 검증 능력" — AI의 답을 그대로 믿지 않고 검증했는가
        - "커뮤니케이션 명확성" — 지시가 명확하고 구체적이었는가 ("알아서 해줘" 류의 막연한 지시가 많았는지)
        - "기본기·전공 지식" — 대화에 드러난 판단이 기술적으로 타당했는가

        banned_phrases_used에는 "알아서", "그냥 해줘", "다 만들어줘"처럼 막연하게 위임한 실제 발화를 인용한다
        (없으면 빈 배열). good_prompts에는 특히 구체적이고 효과적이었던 실제 프롬프트를 인용하고 왜 좋았는지
        간단히 덧붙인다 (없으면 빈 배열 — 억지로 만들지 않는다).

        deliverable_check에는 작업 폴더 상태와 채점자 전용 확인 기준을 비교해 결과물이 실제로 문제를
        해결했는지 구체적으로 적는다. final_advice에는 다음 연습에서 시도해볼 것을 3~5문장으로 적는다.

        # 출력 형식
        아래 JSON 객체 하나만 출력한다. 앞뒤에 설명을 붙이지 않는다. 모든 문자열은 한국어로 쓴다.

        {
          "title": "이번 연습을 한 줄로 요약한 제목",
          "problem_recap": "이번에 푼 문제를 한 줄로",
          "overall_comment": "3~6문장 총평. 학습 관점에서, 잘한 점과 개선점을 균형 있게.",
          "process_steps": [
            { "step": "문제 재정의", "rating": "충분|부분적|부족", "evidence": "...", "suggestion": "..." }
          ],
          "criteria": [
            { "name": "문제 정의 능력", "rating": "충분|보통|부족", "comment": "...", "tip": "..." }
          ],
          "banned_phrases_used": ["실제 인용"],
          "good_prompts": ["실제 인용 — 왜 좋았는지"],
          "deliverable_check": "결과물이 확인 기준을 충족했는지 구체적으로",
          "final_advice": "다음에 시도해볼 것 3~5문장"
        }

        process_steps에는 반드시 위 6단계를 순서대로 모두 넣는다. criteria에는 반드시 위 5개 항목을
        이름 그대로 모두 넣는다.
        """;
    }

    /// <summary>Plain-text transcript for the evaluation prompt (system messages excluded).</summary>
    public static string RenderTranscript(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            if (m.Role == ChatRole.System) continue;
            sb.AppendLine($"[{m.RoleLabel}] {m.Text}");
            sb.AppendLine();
        }
        return sb.Length == 0 ? "(대화 없음)" : sb.ToString();
    }
}
