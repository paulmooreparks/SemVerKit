# SemVerKit

[![CI](https://img.shields.io/github/actions/workflow/status/paulmooreparks/SemVerKit/ci.yml?branch=main&label=CI&logo=github)](https://github.com/paulmooreparks/SemVerKit/actions/workflows/ci.yml)
[![ParksComputing.SemVer](https://img.shields.io/nuget/vpre/ParksComputing.SemVer?label=ParksComputing.SemVer&logo=nuget)](https://www.nuget.org/packages/ParksComputing.SemVer)
[![License](https://img.shields.io/github/license/paulmooreparks/SemVerKit)](LICENSE)

SemVer 2.0.0 parser, comparator, and CLI for .NET. Written in [Overt](https://github.com/paulmooreparks/Overt), an agent-first programming language that transpiles to C# at build time.

This repo is a worked example of "what Overt can do" for a real, shippable .NET library. Everything under [src/ParksComputing.SemVer/SemVer.ov](src/ParksComputing.SemVer/SemVer.ov) is the actual library implementation in Overt; the `.cs` files are thin adapters and internal helpers.

## Status

Early. The numeric core (`major.minor.patch`) parses, renders, and round-trips, with typed errors for every known failure mode. Prerelease and build-metadata segments, comparison, and bump operations are next up. No stable release; current channel is `0.1.0-dev.*` via nuget.org.

## Install

```xml
<PackageReference Include="ParksComputing.SemVer" Version="0.1.0-*" />
```

## Use

```csharp
using Overt.Runtime;
using Overt.Generated.Semver;
using Version = Overt.Generated.Semver.Version;

var result = Module.parse("1.2.3");
switch (result)
{
    case ResultOk<Version, ParseError> ok:
        Console.WriteLine($"parsed: {Module.display(ok.Value)}");
        break;
    case ResultErr<Version, ParseError> err:
        Console.WriteLine($"error: {Module.describe(err.Error)}");
        break;
}
```

The `using Version = ...` alias sidesteps a collision with `System.Version` under the SDK's implicit usings. A tidier public API (C# facade with a non-colliding type name) is tracked as polish work.

## Process notes

This section will grow as the library gets real. Early observations:

- The first diagnostic I hit was `OV0154`: multi-argument calls in Overt require every argument to be named (`str_starts_with(s = raw, prefix = "0")`, not positional). That's the one-canonical-form rule paying off at the language level: a reader can't misread which argument is which.
- `Version` as a type name collides with `System.Version` under `ImplicitUsings`. That's a C# / .NET platform issue, not an Overt one, but it means library authors writing Overt for .NET consumption need to think about name clash the same way they would writing plain C#.

## License

Apache-2.0. See [LICENSE](LICENSE).
