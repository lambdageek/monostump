# monostump

A Mono AOT compiler MSBuild binlog capture and replay analyzer.

![Syndrome from The Incredibles saying "You sly dog, you got me monologuing"](media/monolog.gif)

## Supported scenarios

The tool works by looking for known Mono AOT compiler invocations (in various flavors) in an MSBuild binlog.

The tool currently supports binlogs produced on macOS, Linux and Windows (using the dotnet CLI or `msbuild` from the CLI, or Visual Studio 2022 or later) for the following project types in .NET 8 or later:

* browser WebAssembly
* Android

(Both of the above use the `MonoAOTCompiler` task from [dotnet/runtime](https://github.com/dotnet/runtime))

Additionally support is planned for ios, tvOS and MacCatalyst binlogs (using the `AOTCompile` task  from [xamarin/xamarin-macios](https://github.com/xamarin/xamarin-macios))

In all cases rather than finding the exact `mono-aot-cross` invocation, the tool instead collects the parameters and files that serve as inputs to the AOT task and stores them in a .zip file together
with a `replay.proj` file.  The `Replay` target of the project file loads the task (also saved in the zip file) and executes it.  The project fragment uses adjusted file paths so that everything (input assemblies, toolchains, etc) comes from the zipfile contents.

### Collecting binlogs from Visual Studio

1. Close Visual Studio
2. Open a "Developer PowerShell for VS 2022" (or whatever version you're using)
3. Set the following environment variables and then run `devenv.exe:

   ```console
   $env:MSBuildDebugEngine = "1"
   $env:MSBUILDDEBUGPATH = "C:\Temp"
   devenv.exe
   ```

   (you can use another directory for the debug path)
4. Clean, then build or publish your solution
5. Find the binlog in the temp directory that corresponds to your build. Usually the binlog with the latest timestamp.
6. Run `monostump` on that log

**Note** to replay a project captured from Visual Studio, it is necessary to run `msbuild replay.proj` (not `dotnet replay.proj`) Since the captured AOT compiler task is targeted for .NET Framework, not modern .NET

## Installation

**WARNING** tool isn't stable yet. no guarantees it won't delete all your files.

### Install from nuget.org

```console
$ dotnet tool install -g lambdageek.monostump --prerelease
```

This will install the tool in `~/.dotnet/tools` which should be on your `PATH`. You can then run the tool as `monostump`.

### Install from a local build

Build and install the tool:

```console
$ dotnet pack
$ dotnet tool install -g lambdageek.monostump --prerelease --add-source ./artifacts/package/release
```

### Uninstallation

```console
$ dotnet tool uninstall -g lambdageek.monostump
```

## Usage

```console
Description:
  Scrapes an MSBuild binlog file for Mono AOT compiler invocations and creates a replayable build.

Usage:
  monostump <binlog> [options]

Arguments:
  <binlog>  The path to the binlog file to scrape.

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information
  -v, --verbose   Print verbose output
  -o, --output    The name of the output file [default: replay.zip]
  -n, --dry-run   Don't write the output file
```

1. Compile your project and create a binlog.  For example, here is a .NET 8.0 blazor-wasm project:

   ```console
   $ mkdir ReplayInput
   $ cd ReplayInput
   $ dotnet new blazorwasm -f net8.0
   $ dotnet publish -p:PublishTrimmed=true -p:RunAOTCompilation=true -bl
   ```

2. Run the tool on the generated msbuild.binlog:

   ```console
   $ monostump msbuild.binlog -o ./out/replay.zip
   ...
         Archived to ./out/replay.zip
   ```

3. Send the `replay.zip` to another computer (of the same architecture) or move it to another directory

   ```console
   $ mkdir -p /tmp/replay-example
   $ cp ./out/replay.zip /tmp/replay-example
   $ cd /tmp/replay-example
   $ unzip -q replay.zip
   ```

4. Use `dotnet build replay.proj` to re-run the AOT compiler task exactly how it was invoked in the original build

   ```console
   $ dotnet run replay.proj -p:ReplayOutputPath=./replay-output
   ...
   ```

5. The resulting AOT outputs should be identical:

   ```console
   $ diff .../ReplayInput/obj/Release/net8.0/wasm/for-publish/ReplayInput.dll.bc ./replay-output/aot-output/ReplayInput.dll.bc
   $ echo $?
   0
   ```
