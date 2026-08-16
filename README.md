# Cloud Design Patterns in C#

The cloud design patterns catalogued by the Microsoft Azure Architecture Center, implemented in C#
on .NET 10 as a place to learn them and a showcase of what each one actually looks like as code.

## Overview

Forty-four patterns, organised into six tiers that build on one another rather than the source
catalogue's alphabetical order. Start with the
[pattern catalogue](docs/cloud-pattern-catalogue-v1.0.0.md).

**Every pattern is self-contained.** One folder holds its implementation, its runnable
demonstration, its tests and its documentation, so you can read one pattern without reading the
project, and lift one out without unpicking it from the rest.

**The examples take no dependencies at all.** Cloud infrastructure — queues, caches, stores, remote
services — is modelled by small fakes that live in the pattern that uses them. There is no SDK, no
Docker, no emulator and no Azure subscription: clone it and run it.

That is a deliberate trade, and it costs fidelity. Every pattern README therefore carries an
**In Azure** section naming the real service its model stands in for, **and saying what the model
does not show**. A demonstration that models a queue and never says so teaches you that you have
seen Service Bus.

## Getting started

```
git clone <repository-url>
cd csharp-project-002-cloud-design-patterns
dotnet build
dotnet test
```

Run any single pattern:

```
dotnet run --project patterns/Retry/src
```

Every demonstration runs to completion and exits. None waits for a keypress, so they work
unchanged in a script, a container or a CI step.

Test one pattern on its own:

```
dotnet test patterns/Retry/tests
```

## Prerequisites

- .NET 10 SDK — confirm with `dotnet --list-sdks`
- Any IDE or editor with .NET support. The solution uses the `.slnx` format, which the .NET 10 SDK
  creates by default and the `dotnet` CLI handles everywhere. A recent Visual Studio 2022 opens it
  directly; an older one does not, and `dotnet sln migrate` will not help, because it converts
  `.sln` to `.slnx` and not the reverse. Building and testing from the CLI works regardless.

## Project structure

```
csharp-project-002-cloud-design-patterns/
├── patterns/                   one self-contained folder per pattern
│   └── Retry/
│       ├── README.md           what the pattern is, and when not to use it
│       ├── docs/               supporting material
│       ├── src/                the implementation, its fakes and its demonstration
│       └── tests/              the tests
├── build/                      shared test source compiled into every test project
├── config/                     documentation governance policy
├── docs/                       the pattern catalogue
├── tools/                      repository checks
├── Directory.Build.props       target framework, nullability, analysis
├── Directory.Build.targets     shared test framework and coverage configuration
├── Directory.Packages.props    package versions
├── coverlet.runsettings        coverage collection
└── csharp-project-002-cloud-design-patterns.slnx
```

What must be identical across every pattern — the target framework, the analysis rules, the package
versions — is declared once at the root and inherited, because forty-four copies of a framework
version is forty-four chances for one to drift.

Each pattern's fakes, by contrast, are **not** shared. Tier 1's patterns each carry their own time
abstraction. That duplication is deliberate: a central type would make a pattern folder impossible
to lift out on its own, which is the thing this layout exists to protect.

## Technical details

| | |
|---|---|
| Target framework | .NET 10 |
| Language version | latest |
| Nullable reference types | enabled |
| Implicit usings | enabled |
| Warnings | treated as errors |
| Analysis level | latest recommended |
| Test framework | xUnit |
| Coverage | coverlet, Cobertura format |
| Runtime dependencies | none |

The pattern projects reference no packages at all. A reference implementation of a design pattern
should be readable without learning a library first, so where a shortcut through a third-party
dependency was available it was not taken.

Analysis runs at `latest-recommended` with warnings as errors, and every diagnostic is fixed rather
than silenced. Where a suppression is genuinely right, it is declared in the individual project
that needs it, with the reason beside the code — never solution wide, which would exempt
forty-three innocent projects for the sake of one.

## Testing

```
dotnet test
```

Every pattern has a test project referencing it. Tests assert what a pattern *guarantees* rather
than how it is currently written, so they survive a reimplementation.

Coverage:

```
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

Nothing is excluded from the measurement — the demonstration entry points are counted alongside the
pattern types, deliberately, because a number that rises through exclusions measures the exclusions
rather than the tests.

## Documentation

Each pattern documents itself in its own folder. The
[catalogue](docs/cloud-pattern-catalogue-v1.0.0.md) lists all forty-four, defines the six tiers,
describes the section order every pattern README follows, and records which are implemented so far.

**A pattern whose catalogue entry is plain text is not done yet; one that links to a README is.**
That is how the remaining work stays visible.

Documentation is checked rather than reviewed by eye: prose conventions, Mermaid diagrams parsed by
Mermaid itself, naming and frontmatter under each pattern's `docs/`, and the catalogue's tier
partition:

```
python tools/tier_check.py
```

## Learning resources

- **Microsoft** — [Cloud Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/)
- **Microsoft** — [Antipatterns for cloud applications](https://learn.microsoft.com/en-us/azure/architecture/antipatterns/)
- **Microsoft** — [Azure Well-Architected Framework](https://learn.microsoft.com/en-us/azure/well-architected/)

## Licence

[MIT](LICENSE). Lift a pattern folder into your own project, change it, ship it — commercially or
otherwise. The one condition is that the copyright notice travels with any substantial portion you
copy, and the software comes with no warranty.
