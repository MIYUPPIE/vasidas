using AutomationStudio.Core.Actions;
using AutomationStudio.UI.ViewModels.ActionEditors;

namespace AutomationStudio.UI.Tests;

public class ActionEditorFactoryTests
{
    /// <summary>Every palette entry must produce an editor whose Build() round-trips to the same action type — this is what would have caught a missing case in the palette or the factory switch.</summary>
    [Theory]
    [MemberData(nameof(AllPaletteEntries))]
    public void PaletteEntry_CreatesEditor_ThatBuildsMatchingActionType(ActionPaletteEntry entry)
    {
        var editor = entry.CreateEditor();
        var action = editor.Build("test-id");

        Assert.Equal(editor.ActionTypeName, action.ActionType);
    }

    [Theory]
    [MemberData(nameof(AllPaletteEntries))]
    public void FromAction_RoundTripsBackToAMatchingEditorType(ActionPaletteEntry entry)
    {
        var original = entry.CreateEditor();
        var action = original.Build("test-id");

        var reloaded = ActionEditorFactory.FromAction(action);

        Assert.Equal(original.GetType(), reloaded.GetType());
        Assert.Equal(action.ActionType, reloaded.ActionTypeName);
    }

    [Fact]
    public void Build_AssignsTheGivenId()
    {
        var editor = new AutomationStudio.UI.ViewModels.ActionEditors.WaitEditorViewModel { Milliseconds = 5 };
        var action = editor.Build("my-id");

        Assert.Equal("my-id", action.Id);
    }

    [Fact]
    public void Palette_HasNoDuplicateLabelsWithinACategory()
    {
        var duplicates = ActionPaletteEntry.All
            .GroupBy(e => (e.Category, e.Label))
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(duplicates);
    }

    public static IEnumerable<object[]> AllPaletteEntries() =>
        ActionPaletteEntry.All.Select(entry => new object[] { entry });
}
