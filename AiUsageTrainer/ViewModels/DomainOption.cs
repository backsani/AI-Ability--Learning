using AiUsageTrainer.Common;

namespace AiUsageTrainer.ViewModels;

public sealed class DomainOption : ObservableObject
{
    private bool _isSelected;

    public DomainOption(string name, bool isCustom = false)
    {
        Name = name;
        IsCustom = isCustom;
    }

    public string Name { get; }
    public bool IsCustom { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class TypeOption : ObservableObject
{
    private bool _isSelected;

    public TypeOption(string key, string name, string shortDesc)
    {
        Key = key;
        Name = name;
        ShortDesc = shortDesc;
    }

    public string Key { get; }
    public string Name { get; }
    public string ShortDesc { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
