namespace AutomationStudio.Abstractions;

/// <summary>Coarse, cross-platform control type classification (section 10). Windows UI Automation
/// and Linux AT-SPI each have their own richer, incompatible role vocabularies; finders map into
/// this common set rather than exposing either one directly.</summary>
public enum ElementType
{
    Unknown,
    Window,
    Pane,
    Button,
    CheckBox,
    RadioButton,
    ComboBox,
    Edit,
    Text,
    Hyperlink,
    Image,
    List,
    ListItem,
    Menu,
    MenuItem,
    Tab,
    TabItem,
    Tree,
    TreeItem,
    ToolBar,
    ProgressBar,
    Slider,
    Document
}

/// <summary>What <see cref="IElementFinder"/> found (section 10's <c>DetectedElement</c>).</summary>
public sealed class DetectedElement
{
    public string? Text { get; init; }
    public required ScreenRect Bounds { get; init; }
    public double Confidence { get; init; } = 1.0;
    public ElementType Type { get; init; } = ElementType.Unknown;
    public string? AutomationId { get; init; }

    /// <summary>
    /// Opaque platform handle (an <c>AutomationElement</c> on Windows, an AT-SPI object reference
    /// on Linux) that lets <see cref="IElementFinder.InvokeAsync"/>/<see cref="IElementFinder.SetTextAsync"/>
    /// act on exactly this element without re-finding it. Only meaningful to the
    /// <see cref="IElementFinder"/> instance that produced it — never inspect or construct it
    /// directly from outside a platform backend.
    /// </summary>
    public object? NativeHandle { get; init; }
}

/// <summary>
/// How to locate an element (section 15's JSON "target" shape: <c>{ "text": "Export" }</c>). At
/// least one field should be set; finders match on whichever are provided.
/// </summary>
public sealed record ElementQuery(string? Text = null, string? AutomationId = null, string? WindowTitleContains = null)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Text is not null) parts.Add($"text='{Text}'");
        if (AutomationId is not null) parts.Add($"automationId='{AutomationId}'");
        if (WindowTitleContains is not null) parts.Add($"window~='{WindowTitleContains}'");
        return parts.Count == 0 ? "(empty query)" : string.Join(", ", parts);
    }
}

/// <summary>The located element doesn't support the requested native automation pattern (e.g. a Button has no text-value pattern). Callers fall back to coordinate-based interaction via <see cref="IDesktopController"/>.</summary>
public sealed class ElementPatternNotSupportedException(string patternName, string elementDescription)
    : Exception($"Element '{elementDescription}' does not support the {patternName} pattern.");

/// <summary>
/// Native UI element lookup and interaction (section 10/42): Windows UI Automation, Linux AT-SPI.
/// The workflow engine only ever asks to find/invoke/set-text on an element by these platform-
/// neutral terms — never which native automation tree or pattern satisfied the request.
/// </summary>
public interface IElementFinder
{
    Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default);

    /// <summary>Invokes the element's default action (e.g. presses a button) via its native automation pattern.</summary>
    /// <exception cref="ElementPatternNotSupportedException">The element has no invoke-style pattern.</exception>
    Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default);

    /// <summary>Sets the element's text value (e.g. a text box) via its native automation pattern.</summary>
    /// <exception cref="ElementPatternNotSupportedException">The element has no text-value pattern.</exception>
    Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default);
}
