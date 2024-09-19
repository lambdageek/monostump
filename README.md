# monostump

A Mono AOT compiler MSBuild binlog capture and replay analyzer.

![Syndrome from The Incredibles saying "You sly dog, you got me monologuing"](media/monolog.gif)

## Design

The tool works by looking for known Mono AOT compiler invocations (in various flavors) in an MSBuild binlog.

The currently supported flavors are:

* `MonoAOTCompiler` task from [dotnet/runtime](https://github.com/dotnet/runtime) used by the .NET `browser-wasm` and `android` workloads
* `AOTCompile` task from [xamarin/xamarin-macios](https://github.com/xamarin/xamarin-macios) used by the .NET `ios`, `tvos` and `maccatalyst` workloads

In each case rather than finding the exact `mono-aot-cross` invocation, the tool instead collects the parameters and files that serve as inputs to the AOT task and stores them in a .zip file together
with a `replay.proj` file.  The `Replay` target of the project file loads the task (also saved in the zip file) and executes it.  The project fragment uses adjusted file paths so that everything (input assemblies, toolchains, etc) comes from the zipfile contents.

## Usage

1. Compile your project and create a binlog.  For example, here is a .NET 8.0 blazor-wasm project:
   ```console
   $ mkdir ReplayInput
   $ cd ReplayInput
   $ dotnet new blazorwasm -f net8.0
   $ dotnet publish -p:PublishTrimmed=true -p:RunAOTCompilation=true -bl
   ```
2. Run the tool on the generated msbuild.binlog:
   ```
   $ dotnet run --project .../monostump.csproj -- msbuild.binlog
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
   ```
