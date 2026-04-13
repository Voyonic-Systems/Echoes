# Echoes.NugetSmokeTest

This project verifies that the source generator works correctly when consumed as a **NuGet package** rather than a project reference. Source generators behave differently in each case — the generator DLL is loaded as an analyzer assembly from the package, which has caused bugs in the past that only surfaced after publishing.

## Why it is excluded from the solution build configurations

The project references `Echoes` and `Echoes.Generator` from the local `bin/Debug` directories via `nuget.config`. Those `.nupkg` files are produced by `GeneratePackageOnBuild=true` during a normal solution build — but NuGet restore runs before any project builds. On a fresh clone the packages do not exist yet, so including this project in the solution build would cause a restore failure.

## How to run it

Build the solution first to produce the packages, then build this project explicitly:

```sh
dotnet build src/Echoes.sln -c Release
dotnet build src/Echoes.NugetSmokeTest/Echoes.NugetSmokeTest.csproj -c Release
```

The `nuget.config` includes both `bin/Debug` and `bin/Release` local feeds so the smoke test works regardless of which configuration was used to build the solution.

In CI this maps naturally to two sequential steps.
