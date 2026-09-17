using System.Collections.Generic;

namespace NextorialTrainer.Models;

public sealed class PracticeConfig
{
    public List<string> Domains { get; set; } = new();
    public List<string> TypeKeys { get; set; } = new();

    public string Difficulty { get; set; } = "일반 실무 수준";

    public bool UseTargetTime { get; set; }
    public int TargetMinutes { get; set; } = 60;

    /// <summary>Which agent CLI ran this session.</summary>
    public string AgentId { get; set; } = "claude";

    /// <summary>Model used for the practice conversation (tools enabled).</summary>
    public string CollabModel { get; set; } = "";

    /// <summary>Model used for problem generation and evaluation (tools disabled).</summary>
    public string EvalModel { get; set; } = "";

    /// <summary>The domain/type combination the planner picked for this session.</summary>
    public string PlannedDomain { get; set; } = "";
    public string PlannedTypeKey { get; set; } = "";

    /// <summary>Problem titles already used for this domain/type, so generation avoids repeats.</summary>
    public List<string> AvoidTitles { get; set; } = new();

    public PracticeConfig Clone() => new()
    {
        Domains = new List<string>(Domains),
        TypeKeys = new List<string>(TypeKeys),
        Difficulty = Difficulty,
        UseTargetTime = UseTargetTime,
        TargetMinutes = TargetMinutes,
        AgentId = AgentId,
        CollabModel = CollabModel,
        EvalModel = EvalModel,
        PlannedDomain = PlannedDomain,
        PlannedTypeKey = PlannedTypeKey,
        AvoidTitles = new List<string>(AvoidTitles)
    };
}
