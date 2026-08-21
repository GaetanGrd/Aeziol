# Aeziol

<p align="center">
  <img src="src/Aeziol.App/Assets/Brand/aeziol-cicada.svg" width="150" alt="Aeziol logo">
</p>

<p align="center">
  <strong>A local audio passage between Discord and Windows.</strong>
</p>

<p align="center">
  <a href="README.fr.md">Lire en français</a>
</p>

[![CI](https://github.com/GaetanGrd/Aeziol/actions/workflows/ci.yml/badge.svg)](https://github.com/GaetanGrd/Aeziol/actions/workflows/ci.yml)

Aeziol is a lightweight Windows 11 application that observes Discord voice state. When a voice connection begins, it routes the global Windows audio output to the selected destination. After leaving voice completely, it restores the exact route that was active before the switch.

> [!WARNING]
> Aeziol is currently in beta. Audio routing operates at system level: check the selected output before an important session and keep an easy way to return to Windows sound settings.

## Features

- local detection of Discord Stable, PTB, Canary, and Development;
- official OAuth authorization with PKCE and the `rpc` and `rpc.voice.read` scopes;
- routing of the Windows Console, Multimedia, and Communications roles;
- transactional restoration after leaving voice, restarting, or crashing;
- priority given to manual changes made by the user;
- device exclusions and a configurable stabilization delay;
- English, French, and Arabic interface localization with RTL support;
- Aeziol themes inspired by the Elgo universe;
- built-in Stable and Beta update channels, SHA-256-verified downloads, and user-confirmed installation;
- rotating local logs with paths, identifiers, and secrets redacted.

Aeziol is neither a bot nor a selfbot. It does not read messages, intercept Discord network traffic, or change Discord's internal audio settings.

## Requirements

- Windows 11 x64, version 21H2 or later;
- Discord Desktop for voice automation;
- no separate .NET installation for the self-contained package.

## Installation

User packages are attached to [GitHub releases](https://github.com/GaetanGrd/Aeziol/releases). A release marked **Draft** is reserved for validation and must not be distributed as a public version.

A public MSIX package must be signed. Verify its Windows signature and the accompanying `.sha256` file before installation.

Self-signed test betas also include an `Aeziol-<version>-signing.cer` file. Before the first installation, import this certificate into **Local Computer → Trusted People**, then verify that its subject is `CN=Aeziol Development`. Administrator rights are required. The test certificate does not replace a publicly trusted signature for a stable release.

## Data and privacy

Aeziol works locally. Configuration, logs, and downloaded update packages remain in `%LOCALAPPDATA%\Aeziol`. Discord tokens are kept by Windows Credential Manager under `Aeziol/DiscordOAuth`. No server or voice-channel identifier is written to the logs. Update checks only query public releases from this GitHub repository.

See [Privacy and local data](docs/privacy.md) for details. Discord authorization can be revoked from Aeziol's settings.

## Development

Requirements: Windows 11 x64 and the .NET `10.0.101` SDK, or a compatible servicing release.

```powershell
dotnet restore Aeziol.slnx
dotnet build Aeziol.slnx -c Release
dotnet test --solution Aeziol.slnx -c Release --timeout 60s
```

The diagnostic probe does not change any setting:

```powershell
dotnet run --project tools/Aeziol.Probe/Aeziol.Probe.csproj -- audio-list
dotnet run --project tools/Aeziol.Probe/Aeziol.Probe.csproj -- discord-processes
```

Main project structure:

```text
src/Aeziol.App                       WPF interface and application lifecycle
src/Aeziol.Core                      Audio rules and transactions
src/Aeziol.Infrastructure.Discord    Local RPC and Discord OAuth
src/Aeziol.Infrastructure.Windows    Windows Core Audio integration
tests/Aeziol.Tests                   Isolated unit and visual tests
tools/Aeziol.Probe                   Read-only diagnostics
packaging                            MSIX build and release tooling
```

Read [CONTRIBUTING.md](CONTRIBUTING.md) before contributing. Do not report vulnerabilities in a public issue; follow [SECURITY.md](SECURITY.md) instead.

## Documentation

The detailed project documentation is currently maintained in French:

- [Architecture](docs/architecture.md)
- [Discord application setup](docs/discord-setup.md)
- [Privacy and local data](docs/privacy.md)
- [Adding a language](docs/localization.md)
- [Building and preparing a release](docs/releasing.md)
- [Changelog](CHANGELOG.md)
- [Support and diagnostics](SUPPORT.md)

## Preparing a beta

From `main`, with a clean and committed worktree:

```powershell
.\packaging\publish-beta.ps1 -Version 0.9.0-beta.1 -Publish
```

The command restores, builds, and tests in an isolated directory, pushes `main`, creates the tag, and then asks GitHub Actions to produce a **draft prerelease** with its checksum. Without `-Publish`, it only performs local validation.

The complete procedure and optional signing secrets are documented in [docs/releasing.md](docs/releasing.md).

## Credits

Aeziol is created by [GaetanGrd](https://github.com/GaetanGrd), with assistance from artificial-intelligence tools. Product direction, technical decisions, and final validation remain human responsibilities.

The visual identity and part of the vocabulary are inspired by the original **Elgo** universe. The ambient track “Onde dorée” was generated with **Suno**.

## License

Aeziol is **source-available**, but it is not open-source software. Private modification and use are permitted; redistribution of the source code, modified versions, and derivative builds is prohibited without written permission. See [LICENSE.md](LICENSE.md).
