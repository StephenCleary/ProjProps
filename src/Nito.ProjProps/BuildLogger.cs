using System;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1812
internal sealed class BuildLogger : Microsoft.Build.Framework.ILogger
{
    private readonly ILoggerFactory _loggerFactory;

    public BuildLogger(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }
    public string? Parameters { get => null; set { } }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += (_, args) => _loggerFactory.CreateLogger($"{args.SenderName ?? "msbuild"}").LogDebug(args.Message);
    }

    public void Shutdown()
    {
    }
}
#pragma warning restore
