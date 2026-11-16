# Aiursoft NiBot

<a name="nibot"></a>
[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/AiursoftWeb/nibot/blob/master/LICENSE)
[![Pipeline stat](https://badge-proxy.aiursoft.com/aiursoft/NiBot/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/NiBot/-/pipelines)
[![Test Coverage](https://badge-proxy.aiursoft.com/aiursoft/NiBot/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/NiBot/-/pipelines)
[![S360 compliance](https://s360.aiursoft.com/badge.svg?project=aiursoft%2Fnibot)](https://s360.aiursoft.com/SupplyChain?tab=projects&project=aiursoft%2Fnibot&scope=baseline&ecosystem=all)
[![NuGet version](https://img.shields.io/nuget/v/Aiursoft.NiBot.svg)](https://www.nuget.org/packages/Aiursoft.NiBot/)
[![Man hours](https://manhours.aiursoft.com/r/github.com/aiursoftweb/nibot.svg)](https://manhours.aiursoft.com/r/github.com/aiursoftweb/nibot.html)

A cli tool helps you to de-duplicate images in a folder.

## Overview

### What is NiBot?

Compare and de-duplicate local images, or plan and apply incremental image archiving with optional local CLIP models.

NiBot also supports **incremental archiving into existing category folders**:
`archive` compares an incoming image folder against an existing library, skips exact
duplicates, and uses local CLIP image embeddings to recommend categories for new images.
Use `--classification nearest` to choose the closest existing category, or retain the
strict default for uncertain matches. Model installation uses the .NET `archive-model`
command with SHA-256 verification; no Python setup is required.
It produces a JSON plan and an HTML preview; `archive-apply` copies approved items without
overwriting old files. See [incremental archive setup and usage](docs/archive.md).

Suppose you enjoy collecting images and intensively gather a large number of them from various websites to your hard drive every day. Soon, your hard drive accumulates tens of thousands of CG images with different sources and resolutions.

However, you discover that a significant portion of these downloaded CG images are duplicates, occupying precious disk space. Therefore, you urgently need a fast method to identify and remove duplicates from your CG directory. Since the sources of these downloads vary, traditional methods such as searching by file names are clearly inadequate for solving this problem; it's necessary to analyze the content of the images.

## Installation

Install the CLI with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```bash
dotnet tool install --global Aiursoft.NiBot
```

Update an existing installation:

```bash
dotnet tool update --global Aiursoft.NiBot
```

Remove the CLI:

```bash
dotnet tool uninstall --global Aiursoft.NiBot
```

## Usage

Run the command to see available options:

```bash
nibot --help
```

After getting the binary, run it directly in the terminal.

```bash
$ nibot dedup
Option '--path' is required.

Description:
  De-duplicate images in a folder.

Usage:
  nibot dedup [options]

Options:
  -p, --path <path> (REQUIRED)                  Path of the folder to dedup.
  -ds, --duplicate-similar <duplicate-similar>  Similarity bar. This value means two image are considered as duplicates if their similarity is greater than it. Setting too small may cause different images to be considered as 
                                                duplicates. Suggested values: [96-100] [default: 96]
  -r, --recursive                               Recursively search for similar images in subdirectories. [default: False]
  -k, --keep <keep>                             Preference for sorting images by quality to determine which to keep when duplicates are found. Available options: 
                                                Colorful|GrayScale|Newest|Oldest|Smallest|Largest|HighestResolution|LowestResolution. [default: Colorful|HighestResolution|Largest|Newest]
  -a, --action <Delete|MoveToTrash|Nothing>     Action to take when duplicates are found. Available options: Nothing, Delete, MoveToTrash. [default: MoveToTrash]
  -y, --yes                                     No interactive mode. Taking action without asking for confirmation. [default: False]
  -e, --extensions <extensions>                 Extensions of files to dedup. [default: jpg|jpeg|png|jfif]
  -t, --threads <threads>                       Number of threads to use for image indexing. Default is 32. [default: 32]
  -v, --verbose                                 Show detailed log
  -?, -h, --help                                Show help and usage information
```

It will fetch all images in the folder and compare them with each other. If two images are similar enough, it will consider them as duplicates. 

It will pick the best one based the `--keep` option. If the `--action` is set to `Delete`, it will delete the rest of the duplicates. If the `--action` is set to `MoveToTrash`, it will move the rest of the duplicates to the trash.

Without `--yes`, it will preview each photo and ask for confirmation before deleting files.

<a name="install-as-a-class-library"></a>
<a name="use-as-a-library"></a>
### Use as a library

You can also install this tool as a class library. 

```bash
dotnet add package Aiursoft.NiBot.Core
```

Then you can use the `DedupEngine` class to de-duplicate images in your own code.

```csharp
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
    });
    services.AddTransient<DedupEngine>();
    services.AddTransient<ImageHasher>();
    services.AddTransient<BestPhotoSelector>();
    services.AddTransient<FilesHelper>();
    services.AddTaskCanon();
    var sp = services.BuildServiceProvider();
    var dedupEngine = sp.GetRequiredService<DedupEngine>();
```

That's it!

## Configuration

Configuration is supplied through command-line options; no additional application settings file is required. Use `nibot --help` or `nibot <command> --help` for current options. The deduplication defaults and action choices are preserved in Usage.

Inspect the selected action before processing a real library: `Delete` removes duplicate files, `MoveToTrash` moves them to trash, and `Nothing` leaves them in place. Archiving uses its own model, plan and apply options; see the complete archive guide for model installation, digest verification and plan approval.

## Development

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

From the repository root, restore and build:

```bash
dotnet restore Aiursoft.NiBot.sln
dotnet build Aiursoft.NiBot.sln --no-restore
```

<a name="run-locally"></a>
<a name="local-run"></a>
Run the tool from source, with the prerequisites and configuration above:

```bash
dotnet run --project src/Aiursoft.NiBot/Aiursoft.NiBot.csproj -- --help
```

<a name="run-in-microsoft-visual-studio"></a>
In Microsoft Visual Studio, open `Aiursoft.NiBot.sln`, select `Aiursoft.NiBot` as the startup project and press `F5`. Configure the arguments and environment for the task you want to run.

Run the existing tests:

```bash
dotnet test Aiursoft.NiBot.sln --no-restore
```


## Documentation

- [Incremental archive setup and usage](https://github.com/AiursoftWeb/nibot/blob/master/docs/archive.md)

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you have push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.

## License

Licensed under the [MIT License](https://github.com/AiursoftWeb/nibot/blob/master/LICENSE).
