using System.Collections.Generic;

namespace AiUsageTrainer.Models;

public sealed class AppSettings
{
    /// <summary>Id of the agent CLI in use (see CliAgentCatalog).</summary>
    public string SelectedAgentId { get; set; } = "claude";

    /// <summary>Per-agent path, models and template overrides, keyed by agent id.</summary>
    public Dictionary<string, AgentSettings> Agents { get; set; } = new();

    /// <summary>Light, Dark or System.</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>Extra domains the user typed in themselves.</summary>
    public List<string> CustomDomains { get; set; } = new();

    /// <summary>Domains ticked the last time a practice session was configured.</summary>
    public List<string> LastSelectedDomains { get; set; } = new();

    /// <summary>Problem type keys ticked last time (see ProblemTypeCatalog).</summary>
    public List<string> LastSelectedTypes { get; set; } = new();

    public string LastDifficulty { get; set; } = "일반 실무 수준";

    public bool LastUseTargetTime { get; set; } = false;
    public int LastTargetMinutes { get; set; } = 60;

    public AgentSettings AgentFor(string id)
    {
        if (!Agents.TryGetValue(id, out var s))
        {
            s = new AgentSettings();
            Agents[id] = s;
        }
        return s;
    }
}
