namespace AutomationStudio.Applications.FileManager;

/// <summary>High-level file manager capabilities (DAS.md section 14's "More Applications" list) — Windows File Explorer or, on Linux, GNOME Files/Nautilus (see <see cref="FileManagerProfile"/>).</summary>
public interface IFileManagerPlugin
{
    Task<PluginActionResult> NewFolderAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> CopyAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> CutAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PasteAsync(CancellationToken cancellationToken = default);

    /// <summary>Renames the currently-selected item. Assumes something is already selected — same precondition as pressing F2 by hand.</summary>
    Task<PluginActionResult> RenameAsync(string newName, CancellationToken cancellationToken = default);

    /// <summary>Deletes the currently-selected item(s). Assumes something is already selected.</summary>
    Task<PluginActionResult> DeleteAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> SearchAsync(string query, CancellationToken cancellationToken = default);
}
