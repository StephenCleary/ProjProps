using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;

#pragma warning disable CA1812
internal sealed class ProjPropsOptions
{
    public string? Name { get; set; }
    public string Project { get; set; } = null!;
    public OutputFormatEnum OutputFormat { get; set; }
    public List<(string Name, string Value)> Properties { get; set; } = new List<(string, string)>();
    public bool IncludeEnvironment { get; set; }
    public bool IncludeGlobal { get; set; }
    public bool IncludeImported { get; set; } = true;
    public bool IncludeReserved { get; set; }
    public bool IncludePrimary { get; set; } = true;
    public bool Debug { get; set; }
 
    public static RootCommand DefineOptions(RootCommand command)
    {
        command.AddOption(new Option<string>("--name", "Only include properties whose names match the specified regular expression."));
        command.AddOption(new Option<string>("--project")
        {
            Description = "Project file, or directory containing a single project file.",
            Argument = new Argument<string>(argumentResult =>
            {
                var project = argumentResult.Tokens.SingleOrDefault()?.Value ?? Directory.GetCurrentDirectory();
                if (Directory.Exists(project))
                {
                    string[] projectFiles = Directory.GetFiles(project, "*.*proj");
                    if (projectFiles.Length == 0)
                    {
                        argumentResult.ErrorMessage = $"No project files found in {project}.";
                        return null!;
                    }
                    else if (projectFiles.Length > 1)
                    {
                        argumentResult.ErrorMessage = $"Multiple project files found in {project}.";
                        return null!;
                    }
                    project = projectFiles[0];
                }

                if (!File.Exists(project))
                {
                    argumentResult.ErrorMessage = $"Project {project} not found.";
                    return null!;
                }

                return project;
            }, isDefault: true),
        });
        command.AddOption(new Option<OutputFormatEnum>("--output-format", () => OutputFormatEnum.SingleLinePercentEncode, "The formatting used to display project properties."));
        command.AddOption(new Option<List<(string, string)>>("--properties")
        {
            Description = "Adds an msbuild property: <name>=<value>",
            Argument = new Argument<List<(string, string)>>(
                argumentResult => argumentResult.Tokens.Select(token =>
                {
                    var arg = token.Value;
                    var index = arg.IndexOf('=', StringComparison.Ordinal);
                    var name = arg.Substring(0, index);
                    var value = arg.Substring(index + 1);
                    return (name, value);
                }).ToList())
                .WithArity(ArgumentArity.OneOrMore)
                .WithValidator(argumentResult => argumentResult.Tokens.Select(token =>
                {
                    var arg = token.Value;
                    var index = arg.IndexOf('=', StringComparison.Ordinal);
                    if (index == -1)
                        return $"Property setter {arg} does not contain '='.";
                    if (arg.AsSpan()[0..index].IsWhiteSpace())
                        return $"Invalid property name {arg.Substring(0, index)} for property setter {arg}.";
                    return null;
                }).FirstOrDefault(x => x != null)),
        });
        command.AddOption(new Option<bool>("--include-environment", "Include properties from the environment"));
        command.AddOption(new Option<bool>("--include-global", "Include global properties"));
        command.AddOption(new Option<bool>("--include-imported", () => true, "Include imported properties"));
        command.AddOption(new Option<bool>("--include-reserved", "Include reserved/builtin properties"));
        command.AddOption(new Option<bool>("--include-primary", () => true, "Include primary project properties"));
        command.AddOption(new Option<bool>("--debug", "Enable debug logging"));
        return command;
    }

    public enum OutputFormatEnum
    {
        SingleLinePercentEncode,
        Json,
        SingleValueOnly,
    }
}
#pragma warning restore
