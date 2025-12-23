# Source Generator Notes

## Why Share Files Manually Instead of Using Project References?

Source generators run in an isolated execution context with shadow copying, which prevents them from reliably using standard project references or NuGet packages. When a source generator project references another project, it can cause:

- Build failures due to missing dependencies
- FileNotFoundException errors when assemblies can't be loaded
- Dependency inheritance issues where consuming projects incorrectly inherit the generator's dependencies

The simplest and most reliable approach is to share C# source files directly by copying them into the generator project. This avoids the complex MSBuild configuration required for project references (using `PrivateAssets="all"`, `GetDependencyTargetPaths` targets, etc.) and ensures the code is always available at compile time.

**References:**
- [Source generator project reference discussion](https://github.com/dotnet/roslyn/discussions/47517) - Official explanation of the limitations
- [Using functionality from NuGet packages](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md#use-functionality-from-nuget-packages) - Roslyn cookbook guide

