using System.Collections.Generic;
using System.Linq;

namespace NextorialTrainer.Models;

/// <summary>
/// One of the four problem archetypes inferred from the 넥토리얼 AI 활용 역량평가 분석 —
/// "실제 업무와 유사한 문제"를 AI 도구와 함께 해결하는 시험 형식의 추정 유형.
/// </summary>
public sealed class ProblemType
{
    public ProblemType(string key, string name, string shortDesc, string genGuidance)
    {
        Key = key;
        Name = name;
        ShortDesc = shortDesc;
        GenGuidance = genGuidance;
    }

    public string Key { get; }
    public string Name { get; }
    public string ShortDesc { get; }

    /// <summary>Guidance folded into the problem-generation prompt for this type.</summary>
    public string GenGuidance { get; }
}

public static class ProblemTypeCatalog
{
    public static IReadOnlyList<ProblemType> All { get; } = new List<ProblemType>
    {
        new("bugfix", "버그 수정",
            "재현 조건이 있는 버그를 찾아 고친다",
            "실제로 동작하되 특정 조건에서 잘못된 결과를 내는 코드를 워크스페이스에 파일로 만들어 제시한다. " +
            "버그는 겉으로 명확히 드러나지 않아야 하며(원인이 코드 밖 조건·경계값·동시성 등에 있을 수 있음), " +
            "재현 조건(입력, 실행 방법, 기대 결과 vs 실제 결과)을 구체적으로 서술한다. 정답 코드는 생성물에 포함하지 않는다."),

        new("spec", "스펙 구현",
            "명세된 규칙대로 동작하는 기능을 새로 만든다",
            "입력·출력·제약·예외 케이스가 명확한 스펙 문서를 작성한다. 스펙에는 애매하게 남겨둔 지점을 " +
            "1~2개 의도적으로 포함해, 사용자가 AI에게 되물어 스펙을 구체화하는지 확인할 수 있게 한다."),

        new("data-strategy", "데이터·전략 설계",
            "정답이 없는 조건에서 더 나은 전략을 설계한다",
            "정형화된 정답이 없는 휴리스틱/전략 문제를 낸다. 데이터나 규칙, 제약 조건을 구체적으로 제시하고 " +
            "'더 나은 전략'을 설계하도록 요구한다. 평가 기준(무엇을 최적화하는지)을 명확히 제시하되 " +
            "구현 방법은 열어둔다."),

        new("new-api", "낯선 API·라이브러리 활용",
            "안 써본 라이브러리·기능으로 무언가 만든다",
            "사용자가 평소 쓰지 않을 법한 라이브러리·API·언어 기능을 하나 지정하고, 그것을 이용해 만들어야 " +
            "하는 작은 결과물을 요구한다. 그 API의 핵심 개념을 모르면 바로 풀 수 없게 설계하되, " +
            "공식 문서 없이도 AI에게 물어 학습하며 풀 수 있는 난이도로 제한한다.")
    };

    public static ProblemType? ByKey(string? key) => All.FirstOrDefault(t => t.Key == key);
}
