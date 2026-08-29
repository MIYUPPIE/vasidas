using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

public partial class TypeTextEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _text = "";

    public override string ActionTypeName => "TypeText";
    public override string Summary => string.IsNullOrEmpty(Text) ? "(no text set)" : $"\"{Text}\"";

    public override WorkflowAction Build(string id) => new TypeTextAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, Text = Text
    };

    public static TypeTextEditorViewModel FromAction(TypeTextAction action)
    {
        var vm = new TypeTextEditorViewModel { Text = action.Text };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public partial class PressKeyEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private Key _key = Key.Enter;

    public static IReadOnlyList<Key> AllKeys { get; } = Enum.GetValues<Key>().Where(k => k != Key.None).ToList();

    public override string ActionTypeName => "PressKey";
    public override string Summary => $"Press {Key}";

    public override WorkflowAction Build(string id) => new PressKeyAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, Key = Key
    };

    public static PressKeyEditorViewModel FromAction(PressKeyAction action)
    {
        var vm = new PressKeyEditorViewModel { Key = action.Key };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnKeyChanged(Key value) => OnPropertyChanged(nameof(Summary));
}

public partial class KeyCombinationEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _keysText = "LeftControl, S";

    public override string ActionTypeName => "KeyCombination";
    public override string Summary => string.IsNullOrWhiteSpace(KeysText) ? "(no keys set)" : KeysText;

    public override WorkflowAction Build(string id) => new KeyCombinationAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        Keys = ParseKeys(KeysText)
    };

    public static KeyCombinationEditorViewModel FromAction(KeyCombinationAction action)
    {
        var vm = new KeyCombinationEditorViewModel { KeysText = string.Join(", ", action.Keys) };
        vm.LoadCommonFrom(action);
        return vm;
    }

    /// <summary>Throws <see cref="FormatException"/> for an unrecognised key name — callers building the action (Save/Run) surface it as a validation error.</summary>
    private static IReadOnlyList<Key> ParseKeys(string text)
    {
        var tokens = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new FormatException("Enter at least one key, e.g. \"LeftControl, S\".");
        }

        var keys = new List<Key>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!Enum.TryParse<Key>(token, ignoreCase: true, out var key) || key == Key.None)
            {
                throw new FormatException($"'{token}' is not a recognised key name.");
            }

            keys.Add(key);
        }

        return keys;
    }

    partial void OnKeysTextChanged(string value) => OnPropertyChanged(nameof(Summary));
}
