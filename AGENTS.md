# Agent Guide

Read [ai/README.md](ai/README.md) for task-specific API references.

Before adding, try removing or simplifying. You are done when nothing more can be taken away without losing required behavior, clarity, or verification.

## Build and Test

Run commands from the repository root. The SDK is pinned in [global.json](global.json) to .NET **8.0.100**, with `rollForward: latestFeature` and prereleases disabled. Libraries target `netstandard2.0`; applications and tests target `net8.0`. Check the relevant project file before changing framework-dependent code.

| Task | Command |
|------|---------|
| Restore tools and packages | `dotnet tool restore` then `dotnet paket restore` |
| Build solution (Release, includes restore) | `./build.sh` or `.\build.cmd` |
| Restore only (Windows script) | `.\build.cmd restore` |
| Build one project | `dotnet build src/Aardvark.Geometry.PointSet/Aardvark.Geometry.PointSet.csproj -c Debug` |
| Run tests | `dotnet test src/Aardvark.Algodat.Tests/Aardvark.Algodat.Tests.csproj -c Debug` |
| Filter tests | Append `--filter "FullyQualifiedName~PolyMesh"` to the test command |

`build.sh` has no restore-only mode. Neither build script stops immediately on a failed restore; check restore results before relying on the final build status. There is no `test.sh` or `test.cmd`.

## Dependencies

Use Paket, not `dotnet add package` or hand-edited project package references.

- Add a dependency with `dotnet paket add <package> --project <project>`.
- Change constraints in `paket.dependencies`, then resolve with `dotnet paket install`; `restore` retrieves the existing lock, it does not re-resolve constraints.
- Never edit `paket.lock` manually.
- Test dependencies belong in `group Test` (`net8.0`).

## Scope and Verification

- Modify only task-specific files. Ask before changing `global.json`, dependency constraints, solution files, build scripts, or CI workflows outside the approved task.
- Verify API names and semantics against local source. Dependencies such as Aardvark.Base come from the versions resolved in `paket.lock`, not a sibling checkout.
- Run focused checks for the affected module. For documentation, check links and retained examples as well as `git diff --check`.
