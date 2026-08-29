using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AutomationStudio.Core.Execution;
using AutomationStudio.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;
using AutomationStudio.UI.ViewModels;
using AutomationStudio.UI.Views;
using AutomationStudio.Vision.OCR;

namespace AutomationStudio.UI;

public partial class App : Application
{
    private PlatformAutomationEnvironment? _platformEnvironment;
    private CompositeOcrEngine? _ocrEngine;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var appDataDirectory = GetAppDataDirectory();
            var logDirectory = Path.Combine(appDataDirectory, "logs");

            var loggerFactory = LoggingSetup.CreateLoggerFactory(logDirectory);
            _platformEnvironment = PlatformAutomationEnvironment.Create();
            // OCR (unlike DesktopController/ScreenshotService/etc.) is identical on every platform,
            // so it lives outside PlatformAutomationEnvironment rather than duplicated into both
            // LinuxAutomationEnvironment and WindowsAutomationEnvironment.
            _ocrEngine = new CompositeOcrEngine(loggerFactory.CreateLogger<CompositeOcrEngine>());

            var database = new WorkflowDatabase(Path.Combine(appDataDirectory, "automationstudio.db"));
            var workflowRepository = new WorkflowRepository(database);
            var executionRepository = new ExecutionRepository(database);

            var engine = new WorkflowExecutionEngine(
                _platformEnvironment.DesktopController,
                _platformEnvironment.ScreenshotService,
                _platformEnvironment.WindowManager,
                _platformEnvironment.ElementFinder,
                _ocrEngine,
                loggerFactory.CreateLogger<WorkflowExecutionEngine>(),
                failureScreenshotDirectory: Path.Combine(logDirectory, "failures"));

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(engine, workflowRepository, executionRepository, _platformEnvironment.CapabilityProvider),
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                _platformEnvironment?.Dispose();
                _ocrEngine?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string GetAppDataDirectory()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutomationStudio");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
