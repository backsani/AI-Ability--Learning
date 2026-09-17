using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NextorialTrainer.Models;

public sealed class StarterFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

/// <summary>The concrete problem instance a problem-setter turn produces.</summary>
public sealed class ProblemBrief
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("scenario")]
    public string Scenario { get; set; } = "";

    /// <summary>Full problem statement, markdown. Shown to the user throughout the session.</summary>
    [JsonPropertyName("statement")]
    public string Statement { get; set; } = "";

    /// <summary>Seed files written into the session workspace before the user starts.</summary>
    [JsonPropertyName("starter_files")]
    public List<StarterFile> StarterFiles { get; set; } = new();

    /// <summary>
    /// Grading notes only the evaluator sees during the session. Revealed to the user afterwards
    /// in the report so they can see what the problem was actually checking for.
    /// </summary>
    [JsonPropertyName("hidden_acceptance_criteria")]
    public List<string> HiddenAcceptanceCriteria { get; set; } = new();

    [JsonPropertyName("suggested_time_minutes")]
    public int SuggestedTimeMinutes { get; set; }
}
