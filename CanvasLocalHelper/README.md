# Canvas Local Helper

This cross-platform .NET 10 helper avoids browser CORS restrictions when the Whole Page CPG converter updates Canvas pages.

## Run

From the repository root:

```powershell
dotnet run --project CanvasLocalHelper
```

## Prerequisites

Before running the helper, install the **.NET 10 SDK**. The SDK is the free software required to run this local console app; non-developers should download the installer for their computer from the official Microsoft page:

<https://dotnet.microsoft.com/download/dotnet/10.0>

On the Microsoft download page, choose the **SDK** (not just the Runtime) for your operating system. After installation, open a new PowerShell or Terminal window and confirm it is available by running:

```powershell
dotnet --version
```

The version shown should begin with `10.`. If the command is not recognized, restart the computer and try again.

In Visual Studio, select the `CanvasLocalHelper + index.html` launch profile to start the helper in an external process and open the repository index page in the default browser.

In VS Code with the C# extension or C# Dev Kit installed, select `CanvasLocalHelper + index.html` from **Run and Debug**. The project is built first, `index.html` opens in the default browser, and the helper runs in an external terminal.

The helper prompts for Production, Test, or Beta and then prompts for the Canvas access token without echoing it. It prints a line like:

```text
Helper running at: http://127.0.0.1:49152
Connected Canvas environment: beta (https://usc-online.beta.instructure.com)
```

Copy the helper URL into the converter’s **Local Canvas helper URL** field. Leave the helper window open while using the converter. Stop it with Ctrl+C; the token and environment are then discarded from memory.

For distribution, publish a self-contained executable for each platform, for example:

```text
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
```

The helper binds only to `127.0.0.1` and accepts browser requests only from the published GitHub Pages origin or localhost development origins.
