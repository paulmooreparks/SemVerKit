# SemVerKit

[![CI](https://img.shields.io/github/actions/workflow/status/paulmooreparks/SemVerKit/ci.yml?branch=main&label=CI&logo=github)](https://github.com/paulmooreparks/SemVerKit/actions/workflows/ci.yml)
[![ParksComputing.SemVer](https://img.shields.io/nuget/vpre/ParksComputing.SemVer?label=ParksComputing.SemVer&logo=nuget)](https://www.nuget.org/packages/ParksComputing.SemVer)
[![License](https://img.shields.io/github/license/paulmooreparks/SemVerKit)](LICENSE)

SemVer 2.0.0 parser, comparator, bump operations, and CLI for .NET. Written in [Overt](https://github.com/paulmooreparks/Overt), an agent-first programming language that transpiles to C# at build time. The library implementation in [src/ParksComputing.SemVer/SemVer.ov](src/ParksComputing.SemVer/SemVer.ov) is the entire surface; no project-local C# helpers, no Strings.cs shim. BCL operations come in through `extern "csharp" use` bulk imports.

## Status

Feature-complete for the SemVer 2.0.0 spec:

- **Parse** the full grammar — `MAJOR.MINOR.PATCH(-PRERELEASE)?(+BUILD)?` — with a closed `ParseError` enum naming every distinct failure mode.
- **Display** round-trips: `display(parse(s)) == s` for every accepting input.
- **Compare** by spec precedence rules (numeric core, prerelease identifier-by-identifier with numeric < alphanumeric, build metadata ignored).
- **Bump** major, minor, or patch; result resets prerelease and build to empty.
- **CLI** — `ovsemver` subcommands for `parse` / `compare` / `bump`, with cmp-style exit codes.

98 tests passing (80 library + 18 CLI). No stable release yet; current channel is `0.1.0-dev.*` via nuget.org.

## Install

Library:

```xml
<PackageReference Include="ParksComputing.SemVer" Version="0.1.0-*" />
```

CLI as a global tool:

```sh
dotnet tool install -g ParksComputing.SemVer.Cli
ovsemver --help
```

## Use the library

```csharp
using Overt.Runtime;
using ParksComputing.SemVer;
using Version = ParksComputing.SemVer.Version;

var result = Module.parse("1.2.3-rc.1+build.7");
switch (result) {
    case ResultOk<Version, ParseError> ok:
        Console.WriteLine($"parsed: {Module.display(ok.Value)}");

        var bumped = Module.bump_minor(ok.Value);
        Console.WriteLine($"bumped: {Module.display(bumped)}");   // 1.3.0

        var older = Module.parse("1.0.0").Unwrap();
        var ord = Module.compare(older, bumped);
        Console.WriteLine(ord switch {
            Ordering_Less    => "older < bumped",
            Ordering_Equal   => "older = bumped",
            Ordering_Greater => "older > bumped",
            _                => "(unreachable)",
        });
        break;

    case ResultErr<Version, ParseError> err:
        Console.WriteLine($"error: {Module.describe(err.Error)}");
        break;
}
```

The `using Version = ParksComputing.SemVer.Version;` alias sidesteps a name collision with `System.Version` under the SDK's implicit usings.

## Use the CLI

```sh
$ ovsemver parse 1.2.3-rc.1+build.7
1.2.3-rc.1+build.7

$ ovsemver compare 1.0.0-alpha 1.0.0
<                # also exits 1; cmp-style: 1 less, 0 equal, 2 greater

$ ovsemver bump major 1.2.3-rc.1
2.0.0

$ ovsemver parse 01.2.3
ovsemver parse: major segment '01' has a leading zero; SemVer numeric segments must not be zero-padded
# (exit 1; stdout empty)
```

Exit-code contract: 0 on success, 1 on a domain error (invalid input), 2 on usage errors. `compare` overloads its exit code with the comparison result so shell scripts can branch without parsing stdout.

## Why this is in Overt

SemVer is a small grammar with sharp invariants — no leading zeros on numeric identifiers, a closed alphanumeric character set, identifier slots that must not be empty. That's a good fit for the language features Overt leans on:

- **Refinement types** for the numeric segments. `type NumericSegment = Int where 0 <= self` enforces the non-negative invariant at construction time. Downstream code (`compare`, `bump_*`, `display`) never has to defend against a negative major / minor / patch.
- **Closed enums + exhaustive match** for `ParseError` and `PrereleaseId`. Every failure mode names itself; adding a new variant is a compile error at every match site that doesn't handle it. The `describe` function is the single source of human-readable error messages.
- **`Result<T, E>`** as the only fallibility channel. No exceptions cross the public surface; callers use the same `?` propagation idiom for parse errors as they would for any other domain failure.
- **`extern "csharp" use "..."`** bulk imports for the BCL operations the parser needs. `System.String` instance methods (`starts_with`, `index_of`, `substring`), `System.Int32.Parse`, `System.String.CompareOrdinal` for ordering — all reached through aliased imports, no project-local C# bridge file.

The shape is "Overt source describes the contract; the build pipeline turns it into C# the rest of your .NET project consumes as a normal library."

## Process notes

The journey produced more value as Overt feedback than as SemVer code. Highlights:

- **Bulk imports retired the C# shim.** An earlier iteration of this library carried a 60-line `Strings.cs` for `IsDigits`, `IsAlnumDash`, `Split`, `Join`, etc. — all gone. The replacement is a few `extern "csharp" use "System.String" as str` lines plus four prelude additions in Overt itself (`List.at`, `String.split`, `String.join`, `String.code_at`). The library is now a single `.ov` file with no companion C#. That work also surfaced a typer bug (Some/None patterns through aliased imports) and an emitter gap (named-args inside `${}` interpolation), both fixed upstream.
- **One canonical form.** Multi-argument Overt calls require named arguments (`bump_major(v = parsed)`, not positional). When working through the SemVer ordering rules — numeric vs alphanumeric prerelease, list-prefix tiebreaks, build-metadata-ignored — every call site reads at the eye-tracking level rather than the documentation-cross-reference level. That's payoff at the language level for the rule.
- **`Result` makes the failure shape explicit.** `parse: String -> Result<Version, ParseError>` is the entire fallibility story. The CLI's exit-code contract drops out of the type — domain errors are the `Err` branch, usage errors are everything else. Exception handling exists nowhere in the library.
- **Refinement types catch the impossible.** Constructing a `Version` with a negative `major` is a runtime refinement violation, not a silently-wrong value that propagates. The check happens once at construction; everywhere downstream operates on values already known to be non-negative.

## Layout

```
src/
  ParksComputing.SemVer/        library (one .ov file + csproj)
  ParksComputing.SemVer.Cli/    `ovsemver` global-tool wrapper
tests/
  ParksComputing.SemVer.Tests/      80 tests against the library
  ParksComputing.SemVer.Cli.Tests/  18 tests against the CLI
```

## Releasing

Both packages share the version in [VERSION](VERSION) (MAJOR.MINOR; the patch slot is always `.0`, the channel disambiguates via the prerelease suffix on the tag). The two artifacts:

- `ParksComputing.SemVer` — the library
- `ParksComputing.SemVer.Cli` — the `ovsemver` global tool

The release pipeline is tag-driven. Tag shape decides the channel:

| Tag             | Channel | Publishes |
|-----------------|---------|-----------|
| `v0.1.0-dev.N`  | dev     | nuget.org |
| `v0.1.0-beta.N` | beta    | nuget.org |
| `v0.1.0`        | stable  | nuget.org |

Three triggers:

- **Push a tag** — [release.yml](.github/workflows/release.yml) picks it up, builds, tests, packs both packages, pushes them to nuget.org with `--skip-duplicate`, and creates a GitHub Release with the `.nupkg` files attached.
- **`workflow_dispatch` on release.yml** — scans existing `vMAJOR.MINOR.0-dev.N` tags, computes the next dev counter, atomically reserves the new tag on the remote, and runs the same build.
- **`workflow_dispatch` on [promote.yml](.github/workflows/promote.yml)** — promote a known-healthy `dev.N` tag to `beta.N`, or a `beta.N` tag to a stable `vMAJOR.MINOR.PATCH`. Promotion creates the new tag at the source commit and calls release.yml via `workflow_call` (a `GITHUB_TOKEN`-pushed tag would otherwise not trigger `on.push.tags`).

The pipeline reads `NUGET_API_KEY` from repo secrets; both packages embed the repo-root README so the nuget.org listings render install + usage docs directly.

## License

Apache-2.0. See [LICENSE](LICENSE).
