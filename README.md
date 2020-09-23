![Logo](src/icon.png)

# Nito.ProjProps [![Build status](https://github.com/StephenCleary/ProjProps/workflows/Build/badge.svg)](https://github.com/StephenCleary/ProjProps/actions?query=workflow%3ABuild) [![NuGet version](https://badge.fury.io/nu/Nito.ProjProps.svg)](https://www.nuget.org/packages/Nito.ProjProps)
A dotnet tool that displays project properties.

# Installation

Install the [Nito.ProjProps](https://www.nuget.org/packages/Nito.ProjProps) package.

# Usage

`projprops` is a dotnet cli tool that evaluates and displays MSBuild project properties. It is primarily used in build scripts or devops scenarios.

E.g., `projprops --name version` will evaluate the project file in the current directory and display its `Version` property. The project file is evaluated, so the project file may set `Version` directly, or `Version` may be inferred from `VersionPrefix` and `VersionSuffix`, or `Version` may be set by `Directory.Build.props`, etc.

## Specifying a Project

`projprops` evaluates a single project, similar to how `dotnet run` works. `projprops` will evaluate the project in the current directory by default, or the project specified with the `--project` command line parameter.

## Filtering Output

By default, `projprops` displays all properties for the current project, including properties imported from SDK or other files, but excluding all other properties. You can include environment variable properties, global properties, or reserved properties by specifying the appropriate command line options (e.g., `--include-reserved`).

You can also filter properties by name. The `--name` command line parameter takes a case-insensitive regular expression, and only properties matching that expression are displayed. `projprops --name ".*pack.*"` displays all properties with `"pack"` in the name, and `projprops --name version` displays only the `Version` property.

## Formatting Output

By default, `projprops` displays each property on a single line, with any non-printable or non-ASCII characters displayed as percent-encoded UTF-16 code points, e.g., `%000A%000D` for `\r\n`.

The `--output-format` option allows specifying `Json`, which displays all properties as a single JSON object, with the property names as keys and the property values as the string values of the JSON object.

`--output-format` can also specify `SingleValueOnly`, which only displays the value (unencoded) of a single property, which is useful for scripting scenarios. This option is generally used with `--name` to ensure only one property matches, e.g., `projprops --name version --output-format SingleValueOnly`.

## Global Properties

`projprops` can set global properties via the `--properties` command line argument. E.g., `projprops --properties Configuration=Release CI=true`.

## Debugging

If you want voluminous output to track down what's going wrong, pass the `--debug` command line option. You might want to pipe it to a file, because this will generate a lot of output.
