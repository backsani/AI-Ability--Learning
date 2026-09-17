using System;

namespace NextorialTrainer.Models;

public enum ChatRole
{
    Collaborator,
    User,
    System
}

public sealed class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Text { get; set; } = "";

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public bool IsUser => Role == ChatRole.User;
    public bool IsCollaborator => Role == ChatRole.Collaborator;
    public bool IsSystem => Role == ChatRole.System;

    public string RoleLabel => Role switch
    {
        ChatRole.Collaborator => "협업 AI",
        ChatRole.User => "나",
        _ => "안내"
    };

    public string TimeLabel => Timestamp.ToString("HH:mm");
}
