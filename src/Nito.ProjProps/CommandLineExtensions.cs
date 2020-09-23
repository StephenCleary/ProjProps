using System.CommandLine;
using System.CommandLine.Parsing;

internal static class CommandLineExtensions
{
    public static Argument<T> WithValidator<T>(this Argument<T> @this, ValidateSymbol<ArgumentResult> validator)
    {
        @this.AddValidator(validator);
        return @this;
    }

    public static Argument<T> WithArity<T>(this Argument<T> @this, IArgumentArity arity)
    {
        @this.Arity = arity;
        return @this;
    }
}