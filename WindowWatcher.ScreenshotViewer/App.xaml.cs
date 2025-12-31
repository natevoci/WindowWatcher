using System.Windows;

namespace WindowWatcher.ScreenshotViewer;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var screenshotsFolder = GetScreenshotsFolderArgument(e.Args);
		var window = new ScreenshotViewerWindow(screenshotsFolder);
		window.Show();
	}

	private static string? GetScreenshotsFolderArgument(string[] args)
	{
		const string argumentName = "ScreenshotsFolder";

		for (int index = 0; index < args.Length; index++)
		{
			if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(args[index], $"--{argumentName}", StringComparison.OrdinalIgnoreCase))
			{
				return index + 1 < args.Length && !string.IsNullOrWhiteSpace(args[index + 1])
					? args[index + 1]
					: null;
			}

			if (args[index].StartsWith($"{argumentName}=", StringComparison.OrdinalIgnoreCase)
				|| args[index].StartsWith($"--{argumentName}=", StringComparison.OrdinalIgnoreCase))
			{
				return args[index][(args[index].IndexOf('=') + 1)..];
			}
		}

		return null;
	}
}
