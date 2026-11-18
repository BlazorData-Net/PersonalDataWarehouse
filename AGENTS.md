# Instructions for AI coding agents

## .NET MAUI CLI

Use the .NET MAUI CLI (`maui`) for environment checks, devices and app automation. It's a .NET global tool from
[dotnet/maui-labs](https://github.com/dotnet/maui-labs). It is experimental, so its commands can change between releases;
check `maui --help` if a command below fails.

Install or update it:

```bash
dotnet tool install -g Microsoft.Maui.Cli --prerelease
```

```bash
dotnet tool update -g Microsoft.Maui.Cli --prerelease
```

| Task | Command |
|---|---|
| Check the MAUI environment (SDK, workloads) before building, and after an SDK or Visual Studio update | `maui doctor` |
| Show the CLI version | `maui version` |
| List devices and emulators (add `--json` for scripts) | `maui device list` |
| Profile app startup | `maui profile startup` |
| Inspect a running app: visual tree, screenshot, Blazor WebView | `maui devflow MAUI tree`, `maui devflow MAUI screenshot`, `maui devflow cdp` |

Rules:

- Build and test with `dotnet build` and `dotnet test`. The MAUI CLI does not replace them.
- The MAUI app is `PersonalDataWarehouse.csproj` and its Windows target is `net9.0-windows10.0.19041.0`. This project is on net9.0, so run it with
  `dotnet build PersonalDataWarehouse.csproj -f net9.0-windows10.0.19041.0 -t:Run`. After the project moves to .NET 11, use
  `dotnet run --project PersonalDataWarehouse.csproj -f <net11 Windows TFM>` instead, and update this line.
- If the MAUI build fails on the environment (for example `NETSDK1147`), run `maui doctor` first. Install only the workloads
  the build needs (`maui-windows` for Windows); don't run `dotnet workload restore`, which also pulls in mobile workloads.
- `maui devflow` needs the `Microsoft.Maui.DevFlow.Agent` package in the app. Don't add it unless asked, and never ship it in a Release build.
