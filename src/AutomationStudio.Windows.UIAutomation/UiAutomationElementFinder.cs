using System.Runtime.Versioning;
using System.Windows.Automation;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Windows;

/// <summary>
/// Windows <see cref="IElementFinder"/> via UI Automation (section 4/10/42) — the same
/// <c>System.Windows.Automation</c> managed client Microsoft ships for this exact purpose.
/// </summary>
/// <remarks>
/// Deliberately not raw COM interop against <c>IUIAutomation</c>, unlike the rest of this project's
/// Windows backend (which is plain Win32 P/Invoke, chosen specifically so it compiles without the
/// Windows Desktop SDK). COM interop here would mean declaring UI Automation's interface GUIDs by
/// hand with no way to verify them from this Linux dev machine — a wrong GUID fails silently at
/// runtime with no local way to catch it. The managed wrapper needs <c>UseWPF</c> (see the project
/// file), which does compile here via <c>EnableWindowsTargeting</c>, and eliminates that entire risk
/// class in exchange for a heavier reference. All synchronous UI Automation calls are wrapped in
/// <c>Task.Run</c> — the API is COM-based and can block for cross-process round trips.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class UiAutomationElementFinder : IElementFinder
{
    public Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
        Task.Run(() => FindElementCore(query), cancellationToken);

    public Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
        Task.Run(() => FindAllElementsCore(query), cancellationToken);

    public Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var native = RequireHandle(element);
            if (!native.TryGetCurrentPattern(InvokePattern.Pattern, out var patternObject))
            {
                throw new ElementPatternNotSupportedException("Invoke", element.Text ?? "(unnamed)");
            }

            ((InvokePattern)patternObject).Invoke();
        }, cancellationToken);

    public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var native = RequireHandle(element);
            if (!native.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
            {
                throw new ElementPatternNotSupportedException("Value", element.Text ?? "(unnamed)");
            }

            ((ValuePattern)patternObject).SetValue(text);
        }, cancellationToken);

    private static DetectedElement? FindElementCore(ElementQuery query)
    {
        var condition = BuildCondition(query);
        if (condition is null)
        {
            return null;
        }

        var searchRoot = GetSearchRoot(query);
        var found = searchRoot?.FindFirst(TreeScope.Descendants, condition);
        return found is null ? null : BuildDetectedElement(found);
    }

    private static IReadOnlyList<DetectedElement> FindAllElementsCore(ElementQuery query)
    {
        var condition = BuildCondition(query);
        var searchRoot = condition is null ? null : GetSearchRoot(query);
        if (condition is null || searchRoot is null)
        {
            return [];
        }

        var results = new List<DetectedElement>();
        foreach (AutomationElement element in searchRoot.FindAll(TreeScope.Descendants, condition))
        {
            results.Add(BuildDetectedElement(element));
        }

        return results;
    }

    private static AutomationElement? GetSearchRoot(ElementQuery query)
    {
        if (query.WindowTitleContains is null)
        {
            return AutomationElement.RootElement;
        }

        foreach (AutomationElement window in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition))
        {
            var name = SafeGetName(window);
            if (name.Contains(query.WindowTitleContains, StringComparison.OrdinalIgnoreCase))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>Null when the query has neither Text nor AutomationId set — an unconstrained search would otherwise match every element on the desktop.</summary>
    private static Condition? BuildCondition(ElementQuery query)
    {
        var conditions = new List<Condition>(2);
        if (query.Text is not null)
        {
            conditions.Add(new PropertyCondition(AutomationElement.NameProperty, query.Text));
        }

        if (query.AutomationId is not null)
        {
            conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, query.AutomationId));
        }

        return conditions.Count switch
        {
            0 => null,
            1 => conditions[0],
            _ => new AndCondition([.. conditions])
        };
    }

    private static DetectedElement BuildDetectedElement(AutomationElement element)
    {
        var current = element.Current;
        var rect = current.BoundingRectangle;

        return new DetectedElement
        {
            Text = current.Name,
            Bounds = rect.IsEmpty
                ? new ScreenRect(0, 0, 0, 0)
                : new ScreenRect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height),
            Type = MapControlType(current.ControlType),
            AutomationId = string.IsNullOrEmpty(current.AutomationId) ? null : current.AutomationId,
            NativeHandle = element
        };
    }

    private static string SafeGetName(AutomationElement element)
    {
        try
        {
            return element.Current.Name ?? "";
        }
        catch (ElementNotAvailableException)
        {
            return "";
        }
    }

    private static AutomationElement RequireHandle(DetectedElement element) =>
        element.NativeHandle as AutomationElement
            ?? throw new InvalidOperationException("This DetectedElement was not produced by UiAutomationElementFinder (no native handle).");

    private static ElementType MapControlType(ControlType controlType)
    {
        if (controlType == ControlType.Window) return ElementType.Window;
        if (controlType == ControlType.Pane || controlType == ControlType.Group || controlType == ControlType.Custom) return ElementType.Pane;
        if (controlType == ControlType.Button || controlType == ControlType.SplitButton) return ElementType.Button;
        if (controlType == ControlType.CheckBox) return ElementType.CheckBox;
        if (controlType == ControlType.RadioButton) return ElementType.RadioButton;
        if (controlType == ControlType.ComboBox) return ElementType.ComboBox;
        if (controlType == ControlType.Edit) return ElementType.Edit;
        if (controlType == ControlType.Text) return ElementType.Text;
        if (controlType == ControlType.Hyperlink) return ElementType.Hyperlink;
        if (controlType == ControlType.Image) return ElementType.Image;
        if (controlType == ControlType.List) return ElementType.List;
        if (controlType == ControlType.ListItem || controlType == ControlType.DataItem) return ElementType.ListItem;
        if (controlType == ControlType.Menu || controlType == ControlType.MenuBar) return ElementType.Menu;
        if (controlType == ControlType.MenuItem) return ElementType.MenuItem;
        if (controlType == ControlType.Tab) return ElementType.Tab;
        if (controlType == ControlType.TabItem) return ElementType.TabItem;
        if (controlType == ControlType.Tree) return ElementType.Tree;
        if (controlType == ControlType.TreeItem) return ElementType.TreeItem;
        if (controlType == ControlType.ToolBar) return ElementType.ToolBar;
        if (controlType == ControlType.ProgressBar) return ElementType.ProgressBar;
        if (controlType == ControlType.Slider) return ElementType.Slider;
        if (controlType == ControlType.Document) return ElementType.Document;
        return ElementType.Unknown;
    }
}
