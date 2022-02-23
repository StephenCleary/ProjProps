using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Buildalyzer;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thinktecture.Extensions.Configuration;

#pragma warning disable CA1812
internal sealed class Worker
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<Worker> _logger;
    private readonly ProjPropsOptions _options;
    private readonly Regex? _nameRegex;

    public Worker(
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<Worker> logger,
        IOptions<ProjPropsOptions> options,
        LoggingConfiguration runtimeLoggingConfiguration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _options = options.Value;
        _nameRegex = _options.Name == null ? null : new Regex($"^{_options.Name}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (_options.Debug)
            runtimeLoggingConfiguration.SetLevel(LogLevel.Debug);
    }

    public int Execute()
    {
        try
        {
            if (!TryFindProject(out var projectFile))
                return -1;

            _logger.LogDebug("Evaluating project {project}", projectFile);
            var project = new AnalyzerManager().GetProject(projectFile);
            if (project == null)
                throw new InvalidOperationException($"Could not find project {projectFile}.");
            var results = project.Build().FirstOrDefault();
            if (results == null)
                throw new InvalidOperationException("No target frameworks detected.");

            var value = results.GetProperty(_options.Name);
            if (value == null)
                throw new InvalidOperationException($"Property {_options.Name} not found for target framework {results.TargetFramework}.");

            Console.Write(value);

            return 0;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore
        {
            _logger.LogCritical(ex, "Fatal error");
            return -1;
        }
        finally
        {
            _hostApplicationLifetime.StopApplication();
        }
    }

    private bool TryFindProject(out string project)
    {
        project = _options.Project;
        if (project == null)
            project = Directory.GetCurrentDirectory();

        if (Directory.Exists(project))
        {
            string[] projectFiles = Directory.GetFiles(project, "*.*proj", SearchOption.AllDirectories);
            if (projectFiles.Length == 0)
            {
                _logger.LogCritical($"No project files found in {project}.");
                return false;
            }
            project = projectFiles[0];
        }

        if (!File.Exists(project))
        {
            _logger.LogCritical($"Project {project} not found.");
            return false;
        }

        return true;
    }
}
#pragma warning restore
