using Calculator.Expressions;
using Calculator.Expressions.WolframAlpha;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;

namespace Calculator.Client.WinUI3;

public partial class App : Application
{
    private readonly ServiceProvider _services;

    private Window? _window;

    public App()
    {
        LoadEnvironmentFile();
        InitializeComponent();

        _services = BuildServices();

        // HACK: fix for sluggish UI on Windows 11, see https://github.com/microsoft/microsoft-ui-xaml/issues/11144
        CompositionTarget.Rendering += (_, _) => { };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = _services.GetRequiredService<MainWindow>();
        _window.Closed += (_, _) => _services.Dispose();
        _window.Activate();
    }

    /// <summary>
    /// Settles what the window is given: every way of working an expression out that this build
    /// can offer, in the order they are offered in.
    /// </summary>
    private static ServiceProvider BuildServices()
    {
        var configuration = BuildConfiguration();

        var services = new ServiceCollection();

        // The first registered is the one the window opens on, so it is the one most expressions
        // want: arithmetic done here, at once, in the number type a calculator ordinarily uses.
        services.AddSingleton<IExpressionEvaluator, DecimalExpressionEvaluator>();
        services.AddSingleton<IExpressionEvaluator, BigIntegerExpressionEvaluator>();

        AddWolframAlphaIfConfigured(services, configuration);

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Reads the settings from beside the exe, and then from the environment, which wins.
    /// </summary>
    /// <remarks>
    /// The app id is a secret and does not belong in a file that is committed, so there are two
    /// places to put it that are not: <c>appsettings.local.json</c>, which git ignores, and the
    /// environment variable <c>WolframAlpha__AppId</c>.
    /// </remarks>
    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

    /// <summary>
    /// Offers Wolfram Alpha only where there is an app id to ask it with.
    /// </summary>
    /// <remarks>
    /// Without one every request is refused, so listing it would be offering the reader a way of
    /// working things out that cannot work. Left out of the settings, it is simply not on the
    /// list, and the app is what it was before.
    /// </remarks>
    private static void AddWolframAlphaIfConfigured(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(WolframAlphaOptions.SectionName);

        if (!string.IsNullOrWhiteSpace(section[nameof(WolframAlphaOptions.AppId)]))
        {
            services.AddWolframAlpha(section);
        }
    }

	/// <summary>
	/// Reads the <c>.env</c> file, where there is one, into this process's environment variables.
	/// </summary>
	/// <remarks>
	/// Nothing here is fatal: a missing file is the ordinary case, and a file that cannot be read
	/// leaves the app running on whatever the environment already holds — which is the same position
	/// it would be in without the file at all. Both are only worth a line to a debugger, since this
	/// happens before there is a window to say it in.
	/// </remarks>
	private static void LoadEnvironmentFile()
	{
		try
		{
			var envFile = File.ReadAllText(".env");
			foreach (var line in envFile.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
			{
				if (line.StartsWith('#'))
				{
					continue;
				}

				var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
				if (parts.Length == 2)
				{
					var key = parts[0];
					var value = parts[1];
					Environment.SetEnvironmentVariable(key, value);
				}
				else
				{
					Debug.WriteLine($"Malformed line in .env file: {line}");
				}
			}
		}
		catch (FileNotFoundException)
		{
		}
	}
}
