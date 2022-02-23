using System;
using System.Collections.Generic;
using System.CommandLine;

#pragma warning disable CA1812
internal sealed class ProjPropsOptions
{
    public string? Name { get; set; }
    public string Project { get; set; } = null!;
    public bool Debug { get; set; }
 
    public static RootCommand DefineOptions(RootCommand command)
    {
        command.AddOption(new Option<string>("--name", "The name of the property to read."));
        command.AddOption(new Option<string>("--project", "Project file or directory."));
        command.AddOption(new Option<bool>("--debug", "Enable debug logging"));
        return command;
    }
}
#pragma warning restore
