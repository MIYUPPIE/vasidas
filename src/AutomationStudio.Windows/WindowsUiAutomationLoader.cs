using System.Reflection;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Windows;

/// <summary>
/// Loads the real UI Automation <see cref="IElementFinder"/> from
/// <c>AutomationStudio.Windows.UIAutomation.dll</c> via reflection at runtime — see that project's
/// own doc comment for why it can't be a normal compile-time reference from here. Deployment must
/// place that DLL next to this app's executable (a Phase 9 installer/packaging concern; not done
/// yet, so running from a raw build output today needs it copied there manually).
/// </summary>
internal static class WindowsUiAutomationLoader
{
    private const string AssemblyFileName = "AutomationStudio.Windows.UIAutomation.dll";
    private const string TypeName = "AutomationStudio.Windows.UiAutomationElementFinder";

    public static IElementFinder Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, AssemblyFileName);
            if (!File.Exists(path))
            {
                return new UnavailableElementFinder(
                    $"'{AssemblyFileName}' was not found next to the running application at '{path}'. " +
                    "It must be deployed alongside the app (a separate assembly specifically so the rest " +
                    "of the app doesn't need the WPF runtime).");
            }

            var assembly = Assembly.LoadFrom(path);
            var type = assembly.GetType(TypeName)
                ?? throw new InvalidOperationException($"Type '{TypeName}' was not found in '{AssemblyFileName}'.");
            if (Activator.CreateInstance(type) is not IElementFinder finder)
            {
                throw new InvalidOperationException($"'{TypeName}' does not implement {nameof(IElementFinder)}.");
            }

            return finder;
        }
        catch (Exception ex)
        {
            return new UnavailableElementFinder($"Failed to load Windows UI Automation: {ex.Message}");
        }
    }

    private sealed class UnavailableElementFinder(string reason) : IElementFinder
    {
        private Exception MakeException() => new CapabilityUnavailableException("Element finding", reason);

        public Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
            throw MakeException();

        public Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
            throw MakeException();

        public Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default) =>
            throw MakeException();

        public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default) =>
            throw MakeException();
    }
}
