using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Thinktecture;
using Thinktecture.Extensions.Configuration;

namespace ProjProps
{
    class Program
    {
        public static async Task<int> Main(string[] args) => await BuildCommandLine()
            .UseHost(
                _ => Host.CreateDefaultBuilder(args).ConfigureAppConfiguration(config =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { "Logging:LogLevel:Microsoft.Extensions.Hosting", "Warning" },
                        { "Logging:LogLevel:Microsoft.Hosting", "Warning" },
                    });
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                }),
                ConfigureHostBuilder)
            .UseDefaults()
            .Build()
            .InvokeAsync(args);

        private static CommandLineBuilder BuildCommandLine()
        {
            var root = new RootCommand("Display project properties.") { Name = "projprops" };
            ProjPropsOptions.DefineOptions(root);
            root.Handler = CommandHandler.Create<IHost>(host => host.Services.GetRequiredService<Worker>().Execute());
            return new CommandLineBuilder(root);
        }

        public static void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            var runtimeLoggingConfiguration = new LoggingConfiguration();
            hostBuilder
                .ConfigureAppConfiguration(config => config.AddLoggingConfiguration(runtimeLoggingConfiguration, "Logging"))
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions<ProjPropsOptions>().BindCommandLine();
                    services.AddSingleton<BuildLogger>();
                    services.AddSingleton<Worker>();
                    services.AddSingleton(runtimeLoggingConfiguration);
                });
        }
    }
}
