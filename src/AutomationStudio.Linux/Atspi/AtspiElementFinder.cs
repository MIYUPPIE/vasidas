using AutomationStudio.Abstractions;
using Tmds.DBus.Protocol;

namespace AutomationStudio.Linux.Atspi;

/// <summary>
/// Linux <see cref="IElementFinder"/> via AT-SPI2 (Assistive Technology Service Provider
/// Interface), the accessibility bus GNOME/GTK/Qt/Chromium/Electron apps all expose their UI
/// element trees through — the design doc's prescribed Linux mechanism (section 4/31).
/// </summary>
/// <remarks>
/// Two things confirmed live on this dev machine before writing this, both important enough to be
/// worth recording:
/// <list type="bullet">
/// <item>AT-SPI lives on its own dedicated D-Bus connection, not the session bus — its address is
/// obtained by calling <c>org.a11y.Bus.GetAddress</c> on the session bus first.</item>
/// <item>Accessibility must actually be enabled for apps to expose a real tree at all (the GNOME
/// <c>org.gnome.desktop.interface toolkit-accessibility</c> setting was off by default on this
/// machine — most Linux desktops ship it off). With it off, AT-SPI's registry still answers, but
/// applications don't bridge their widgets into it. See <see cref="AtspiCapabilityCheck"/>.</item>
/// </list>
/// This only uses <c>CallMethodAsync</c> (request/reply), never D-Bus signals — Phase 2's portal
/// work found real, unresolved bugs in this library's signal reception specifically; plain method
/// calls had no such problems.
/// </remarks>
public sealed class AtspiElementFinder : IElementFinder
{
    private const string RegistryDestination = "org.a11y.atspi.Registry";
    private const string RootPath = "/org/a11y/atspi/accessible/root";
    private const string AccessibleInterface = "org.a11y.atspi.Accessible";
    private const string ComponentInterface = "org.a11y.atspi.Component";
    private const string ActionInterface = "org.a11y.atspi.Action";
    private const string EditableTextInterface = "org.a11y.atspi.EditableText";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    private const int MaxSearchDepth = 40;

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private DBusConnection? _connection;

    internal readonly record struct AccessibleRef(string BusName, string Path);

    public async Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var root in await GetSearchRootsAsync(connection, query, cancellationToken).ConfigureAwait(false))
        {
            var found = await SearchAsync(connection, root, query, depth: 0, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<DetectedElement>();
        foreach (var root in await GetSearchRootsAsync(connection, query, cancellationToken).ConfigureAwait(false))
        {
            await SearchAllAsync(connection, root, query, results, depth: 0, cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public async Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default)
    {
        var node = RequireHandle(element);
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        var interfaces = await GetInterfacesAsync(connection, node, cancellationToken).ConfigureAwait(false);
        if (!interfaces.Contains(ActionInterface))
        {
            throw new ElementPatternNotSupportedException("Action", element.Text ?? "(unnamed)");
        }

        await DoActionAsync(connection, node, 0, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default)
    {
        var node = RequireHandle(element);
        var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        var interfaces = await GetInterfacesAsync(connection, node, cancellationToken).ConfigureAwait(false);
        if (!interfaces.Contains(EditableTextInterface))
        {
            throw new ElementPatternNotSupportedException("EditableText", element.Text ?? "(unnamed)");
        }

        await SetTextContentsAsync(connection, node, text, cancellationToken).ConfigureAwait(false);
    }

    private static AccessibleRef RequireHandle(DetectedElement element) =>
        element.NativeHandle as AccessibleRef?
            ?? throw new InvalidOperationException("This DetectedElement was not produced by AtspiElementFinder (no native handle).");

    private async Task<List<AccessibleRef>> GetSearchRootsAsync(DBusConnection connection, ElementQuery query, CancellationToken cancellationToken)
    {
        var appRoots = await GetChildrenAsync(connection, new AccessibleRef(RegistryDestination, RootPath), cancellationToken).ConfigureAwait(false);

        if (query.WindowTitleContains is null)
        {
            return appRoots;
        }

        // Narrow the search to just the matching top-level window(s) — app roots' direct children
        // are its windows/frames (confirmed live: a two-window Electron app's root had exactly two
        // children, one per window).
        var matches = new List<AccessibleRef>();
        foreach (var appRoot in appRoots)
        {
            foreach (var window in await GetChildrenAsync(connection, appRoot, cancellationToken).ConfigureAwait(false))
            {
                var name = await GetNameAsync(connection, window, cancellationToken).ConfigureAwait(false);
                if (name.Contains(query.WindowTitleContains, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(window);
                }
            }
        }

        return matches;
    }

    private async Task<DetectedElement?> SearchAsync(
        DBusConnection connection, AccessibleRef node, ElementQuery query, int depth, CancellationToken cancellationToken)
    {
        if (depth > MaxSearchDepth)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var name = await GetNameAsync(connection, node, cancellationToken).ConfigureAwait(false);
        if (await MatchesAsync(connection, node, name, query, cancellationToken).ConfigureAwait(false))
        {
            return await BuildDetectedElementAsync(connection, node, name, cancellationToken).ConfigureAwait(false);
        }

        foreach (var child in await GetChildrenAsync(connection, node, cancellationToken).ConfigureAwait(false))
        {
            var found = await SearchAsync(connection, child, query, depth + 1, cancellationToken).ConfigureAwait(false);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private async Task SearchAllAsync(
        DBusConnection connection, AccessibleRef node, ElementQuery query, List<DetectedElement> results, int depth, CancellationToken cancellationToken)
    {
        if (depth > MaxSearchDepth)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var name = await GetNameAsync(connection, node, cancellationToken).ConfigureAwait(false);
        if (await MatchesAsync(connection, node, name, query, cancellationToken).ConfigureAwait(false))
        {
            results.Add(await BuildDetectedElementAsync(connection, node, name, cancellationToken).ConfigureAwait(false));
        }

        foreach (var child in await GetChildrenAsync(connection, node, cancellationToken).ConfigureAwait(false))
        {
            await SearchAllAsync(connection, child, query, results, depth + 1, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> MatchesAsync(DBusConnection connection, AccessibleRef node, string name, ElementQuery query, CancellationToken cancellationToken)
    {
        if (query.Text is not null)
        {
            return string.Equals(name, query.Text, StringComparison.OrdinalIgnoreCase);
        }

        if (query.AutomationId is not null)
        {
            // AT-SPI has no first-class "automation id"; best-effort via object attributes some
            // toolkits populate (e.g. GTK's "id" attribute). Not universally supported.
            var attributes = await GetAttributesAsync(connection, node, cancellationToken).ConfigureAwait(false);
            return attributes.TryGetValue("id", out var id) && string.Equals(id, query.AutomationId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private async Task<DetectedElement> BuildDetectedElementAsync(DBusConnection connection, AccessibleRef node, string name, CancellationToken cancellationToken)
    {
        var roleName = await GetRoleNameAsync(connection, node, cancellationToken).ConfigureAwait(false);
        var bounds = await TryGetExtentsAsync(connection, node, cancellationToken).ConfigureAwait(false);

        return new DetectedElement
        {
            Text = name,
            Bounds = bounds ?? new ScreenRect(0, 0, 0, 0),
            Type = MapRole(roleName),
            NativeHandle = node
        };
    }

    private static ElementType MapRole(string atspiRoleName) => atspiRoleName switch
    {
        "frame" or "window" or "dialog" or "alert" => ElementType.Window,
        "panel" or "filler" or "layered pane" or "scroll pane" or "viewport" or "group" or "generic" => ElementType.Pane,
        "push button" or "toggle button" => ElementType.Button,
        "check box" => ElementType.CheckBox,
        "radio button" => ElementType.RadioButton,
        "combo box" => ElementType.ComboBox,
        "entry" or "password text" or "spin button" => ElementType.Edit,
        "label" or "text" or "static" => ElementType.Text,
        "link" => ElementType.Hyperlink,
        "image" or "icon" => ElementType.Image,
        "list" or "list box" or "grid" => ElementType.List,
        "list item" or "grid cell" or "table cell" => ElementType.ListItem,
        "menu" or "menu bar" => ElementType.Menu,
        "menu item" or "check menu item" or "radio menu item" => ElementType.MenuItem,
        "page tab list" => ElementType.Tab,
        "page tab" => ElementType.TabItem,
        "tree" or "tree table" => ElementType.Tree,
        "tree item" => ElementType.TreeItem,
        "tool bar" => ElementType.ToolBar,
        "progress bar" => ElementType.ProgressBar,
        "slider" => ElementType.Slider,
        "document frame" or "document web" or "document text" => ElementType.Document,
        _ => ElementType.Unknown
    };

    // --- D-Bus plumbing -----------------------------------------------------------------------

    private async Task<DBusConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var sessionAddress = DBusAddress.Session
                ?? throw new InvalidOperationException("No session D-Bus address ($DBUS_SESSION_BUS_ADDRESS is not set).");
            using var sessionConnection = new DBusConnection(sessionAddress);
            await sessionConnection.ConnectAsync().ConfigureAwait(false);

            var atspiAddress = await GetAtspiBusAddressAsync(sessionConnection, cancellationToken).ConfigureAwait(false);

            var atspiConnection = new DBusConnection(atspiAddress);
            await atspiConnection.ConnectAsync().ConfigureAwait(false);
            _connection = atspiConnection;
            return _connection;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private static async Task<string> GetAtspiBusAddressAsync(DBusConnection sessionConnection, CancellationToken cancellationToken)
    {
        var writer = sessionConnection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: "org.a11y.Bus", path: "/org/a11y/bus", @interface: "org.a11y.Bus", member: "GetAddress");
        var message = writer.CreateMessage();
        return await sessionConnection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadString())
            .ConfigureAwait(false);
    }

    private static async Task<List<AccessibleRef>> GetChildrenAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: AccessibleInterface, member: "GetChildren");
        var message = writer.CreateMessage();

        try
        {
            return await connection.CallMethodAsync(message, static (Message m, object? _) =>
            {
                var reader = m.GetBodyReader();
                var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
                var children = new List<AccessibleRef>();
                while (reader.HasNext(arrayEnd))
                {
                    var busName = reader.ReadString();
                    var path = reader.ReadObjectPathAsString();
                    children.Add(new AccessibleRef(busName, path));
                }

                return children;
            }).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            // A child reference can point at an app that has since exited or a node that has been
            // torn down; skip it rather than fail the whole search.
            return [];
        }
    }

    private static async Task<string> GetNameAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        try
        {
            return await GetPropertyAsync(connection, node, AccessibleInterface, "Name", cancellationToken).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            return "";
        }
    }

    private static Task<string> GetRoleNameAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: AccessibleInterface, member: "GetRoleName");
        var message = writer.CreateMessage();
        return connection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadString());
    }

    private static Task<string[]> GetInterfacesAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: AccessibleInterface, member: "GetInterfaces");
        var message = writer.CreateMessage();
        return connection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadArrayOfString());
    }

    private static async Task<Dictionary<string, string>> GetAttributesAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: AccessibleInterface, member: "GetAttributes");
        var message = writer.CreateMessage();
        try
        {
            return await connection.CallMethodAsync(message, static (Message m, object? _) =>
            {
                var reader = m.GetBodyReader();
                var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
                var dict = new Dictionary<string, string>();
                while (reader.HasNext(arrayEnd))
                {
                    var key = reader.ReadString();
                    var value = reader.ReadString();
                    dict[key] = value;
                }

                return dict;
            }).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            return [];
        }
    }

    private static async Task<ScreenRect?> TryGetExtentsAsync(DBusConnection connection, AccessibleRef node, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: ComponentInterface, member: "GetExtents", signature: "u");
        writer.WriteUInt32(0); // CoordType.SCREEN
        var message = writer.CreateMessage();

        try
        {
            return await connection.CallMethodAsync(message, static (Message m, object? _) =>
            {
                var reader = m.GetBodyReader();
                var x = reader.ReadInt32();
                var y = reader.ReadInt32();
                var width = reader.ReadInt32();
                var height = reader.ReadInt32();
                return new ScreenRect(x, y, width, height);
            }).ConfigureAwait(false);
        }
        catch (DBusErrorReplyException)
        {
            // Element has no Component interface (not all accessible objects are on-screen, e.g. menus before opening).
            return null;
        }
    }

    private static Task DoActionAsync(DBusConnection connection, AccessibleRef node, int actionIndex, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: ActionInterface, member: "DoAction", signature: "i");
        writer.WriteInt32(actionIndex);
        var message = writer.CreateMessage();
        return connection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadBool());
    }

    private static Task SetTextContentsAsync(DBusConnection connection, AccessibleRef node, string text, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: EditableTextInterface, member: "SetTextContents", signature: "s");
        writer.WriteString(text);
        var message = writer.CreateMessage();
        return connection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadBool());
    }

    private static async Task<string> GetPropertyAsync(DBusConnection connection, AccessibleRef node, string interfaceName, string propertyName, CancellationToken cancellationToken)
    {
        var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: node.BusName, path: node.Path, @interface: PropertiesInterface, member: "Get", signature: "ss");
        writer.WriteString(interfaceName);
        writer.WriteString(propertyName);
        var message = writer.CreateMessage();
        return await connection.CallMethodAsync(message, static (Message m, object? _) => m.GetBodyReader().ReadVariantValue().GetString())
            .ConfigureAwait(false);
    }
}
