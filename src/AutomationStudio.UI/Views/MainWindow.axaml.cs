using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AutomationStudio.UI.ViewModels;
using AutomationStudio.UI.ViewModels.ActionEditors;

namespace AutomationStudio.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handled in code-behind rather than a relative-parent command binding: each palette button's
    /// DataContext is its own <see cref="ActionPaletteEntry"/>, so reaching the outer
    /// <see cref="MainWindowViewModel.CurrentEditor"/> from inside that DataTemplate is simpler this
    /// way than via nested-binding path navigation.
    /// </summary>
    private void OnAddActionButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ActionPaletteEntry entry })
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.CurrentEditor?.AddActionCommand.CanExecute(entry) == true)
        {
            viewModel.CurrentEditor.AddActionCommand.Execute(entry);
        }
    }

    private async void OnExportJsonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentEditor is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Workflow",
            SuggestedFileName = $"{viewModel.CurrentEditor.Name}.json",
            FileTypeChoices = [new FilePickerFileType("Workflow JSON") { Patterns = ["*.json"] }]
        });

        if (file is not null)
        {
            await viewModel.ExportCurrentWorkflowAsync(file.Path.LocalPath);
        }
    }

    private async void OnImportJsonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Workflow",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Workflow JSON") { Patterns = ["*.json"] }]
        });

        if (files.Count > 0)
        {
            await viewModel.ImportWorkflowAsync(files[0].Path.LocalPath);
        }
    }
}
