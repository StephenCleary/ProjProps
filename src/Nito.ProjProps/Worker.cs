using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thinktecture.Extensions.Configuration;

#pragma warning disable CA1812
internal sealed class Worker
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<Worker> _logger;
    private readonly BuildLogger _buildLogger;
    private readonly ProjPropsOptions _options;
    private readonly Regex? _nameRegex;

    public Worker(
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<Worker> logger,
        BuildLogger buildLogger,
        IOptions<ProjPropsOptions> options,
        LoggingConfiguration runtimeLoggingConfiguration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _buildLogger = buildLogger;
        _options = options.Value;
        _nameRegex = _options.Name == null ? null : new Regex($"^{_options.Name}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (_options.Debug)
            runtimeLoggingConfiguration.SetLevel(LogLevel.Debug);
    }

    public void Execute()
    {
        try
        {
            // Thanks to https://daveaglick.com/posts/running-a-design-time-build-with-msbuild-apis
            string toolsPath = GetCoreToolsPath(_options.Project);
            var environmentSettings = GetCoreEnvironmentSettings(_options.Project, toolsPath);
            
            foreach (var prop in environmentSettings)
                Environment.SetEnvironmentVariable(prop.Key, prop.Value);
            using var collection = new ProjectCollection();
            collection.AddToolset(new Toolset("Current", toolsPath, collection, null));
            collection.RegisterLogger(_buildLogger);
            foreach (var globalProperty in _options.Properties)
                collection.SetGlobalProperty(globalProperty.Name, globalProperty.Value);

            var project = collection.LoadProject(_options.Project);
            var properties = new SortedDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var prop in project.AllEvaluatedProperties.Where(FilterProperty))
                properties[prop.Name] = prop.EvaluatedValue;

            if (_options.OutputFormat == ProjPropsOptions.OutputFormatEnum.SingleValueOnly)
            {
                if (properties.Count != 1)
                    throw new InvalidOperationException("Output is SingleValueOnly but multiple properties matched.");
                Console.Write(properties.First().Value);
            }
            else if (_options.OutputFormat == ProjPropsOptions.OutputFormatEnum.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(properties));
            }
            else if (_options.OutputFormat == ProjPropsOptions.OutputFormatEnum.SingleLinePercentEncode)
            {
                foreach (var prop in properties)
                    Console.WriteLine($"{prop.Key}={PercentEncode(prop.Value)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error");
            throw;
        }
        finally
        {
            _hostApplicationLifetime.StopApplication();
        }
    }

    private static string PercentEncode(string input)
    {
        var result = new StringBuilder(input.Length);
        var bytes = Utf8.GetBytes(input);
        foreach (byte ch in bytes)
        {
            if ((ch >= ' ' && ch <= '$') || (ch >= '&' && ch <= '~'))
                result.Append((char) ch);
            else
                result.Append($"%{ch:X2}");
        }
        return result.ToString();
    }

    private bool FilterProperty(ProjectProperty prop)
    {
        if (prop.IsEnvironmentProperty && _options.ExcludeEnvironment)
        {
            _logger.LogDebug("Skipping {name} because it is an environment property.", prop.Name);
            return false;
        }

        if (prop.IsGlobalProperty && _options.ExcludeGlobal)
        {
            _logger.LogDebug("Skipping {name} because it is a global property.", prop.Name);
            return false;
        }
        
        if (prop.IsImported && _options.ExcludeImported)
        {
            _logger.LogDebug("Skipping {name} because it is an imported property.", prop.Name);
            return false;
        }
        
        if (prop.IsReservedProperty && _options.ExcludeReserved)
        {
            _logger.LogDebug("Skipping {name} because it is a reserved property.", prop.Name);
            return false;
        }
        
        if (prop.IsGlobalProperty && _options.ExcludeGlobal)
        {
            _logger.LogDebug("Skipping {name} because it is a global property.", prop.Name);
            return false;
        }

        if (!prop.IsEnvironmentProperty && !prop.IsGlobalProperty && !prop.IsImported && !prop.IsReservedProperty && _options.ExcludePrimary)
        {
            _logger.LogDebug("Skipping {name} because it is a primary property.", prop.Name);
            return false;
        }

        if (_nameRegex != null && !_nameRegex.IsMatch(prop.Name))
        {
            _logger.LogDebug("Skipping {name} because it does not match {regex}.", prop.Name, _options.Name);
            return false;
        }

        _logger.LogDebug("Processing {name}.", prop.Name);
        return true;
    }

    private static Dictionary<string, string> GetCoreEnvironmentSettings(string projectPath, string toolsPath)
    {
        return new Dictionary<string, string>
        {
            { "SolutionDir", Path.GetDirectoryName(projectPath) ?? "" },
            { "MSBuildExtensionsPath", toolsPath },
            { "MSBuildSDKsPath", Path.Combine(toolsPath, "Sdks") },
            { "RoslynTargetsPath", Path.Combine(toolsPath, "Roslyn") },
            { "GenerateResourceMSBuildArchitecture", "CurrentArchitecture" }, // https://github.com/dotnet/sdk/issues/346#issuecomment-257654120
            { "MSBuildLoadMicrosoftTargetsReadOnly", "true" }, // https://github.com/dotnet/msbuild/blob/580f1bbdc0635fc300ed3922524c60796d725142/src/MSBuild/XMake.cs#L495
            { "MSBUILDLOADALLFILESASREADONLY", "1" }, // https://github.com/dotnet/msbuild/blob/0901979ef341602e31e750db04088f4e7ba09733/src/Build/ElementLocation/XmlDocumentWithLocation.cs#L382
        };
    }

    private string GetCoreToolsPath(string projectPath)
    {
        var startInfo = new ProcessStartInfo("dotnet", "--info")
        {
            // global.json may change the version, so need to set working directory
            WorkingDirectory = Path.GetDirectoryName(projectPath),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                { "DOTNET_CLI_UI_LANGUAGE", "en-US" },
            },
        };

        // Execute the process
        using (Process process = Process.Start(startInfo))
        {
            var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
            process.WaitForExit();
            var stdout = stdoutTask.GetAwaiter().GetResult();
            return ParseCoreBasePath(stdout);
        }
    }

    private string ParseCoreBasePath(string stdout)
    {
        var match = Regex.Match(stdout, @"^\s*Base Path:\s*(.*)$", RegexOptions.Multiline);
        if (!match.Success)
            throw new InvalidOperationException("Could not get base path from `dotnet --info`.");
        var result = match.Groups[1].Value.Trim();
        _logger.LogDebug("dotnet base path: {path}", result);
        return result;
    }
}
#pragma warning restore
