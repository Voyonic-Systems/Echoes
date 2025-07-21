using Microsoft.CodeAnalysis;

namespace Tommy;

public static class Extensions
{
    /// <summary>Gets the file path the source generator was called from.</summary>
    /// <param name="context">The context of the Generator's Execute method.</param>
    /// <returns>The file path the generator was called from.</returns>
    public static string GetCallingPath(this GeneratorExecutionContext context)
    {
        return context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var result)
            ? result 
            : null;
    }
}