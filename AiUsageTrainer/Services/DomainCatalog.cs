using System.Collections.Generic;

namespace AiUsageTrainer.Services;

public static class DomainCatalog
{
    public static IReadOnlyList<string> Presets { get; } = new[]
    {
        "C++",
        "C#",
        "Java",
        "Python",
        "JavaScript·TypeScript",
        "SQL·데이터베이스",
        "알고리즘·자료구조 (언어 무관)",
        "웹 서버·REST API"
    };
}
