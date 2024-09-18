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

