# NiBot

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.com/aiursoft/NiBot/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.com/aiursoft/NiBot/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/NiBot/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.com/aiursoft/NiBot/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/NiBot/-/pipelines)
[![NuGet version](https://img.shields.io/nuget/v/Aiursoft.NiBot.svg)](https://www.nuget.org/packages/Aiursoft.NiBot/)
[![Man hours](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/nibot.svg)](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/nibot.html)

A cli tool helps you to de-duplicate images in a folder.

Suppose you enjoy collecting images and intensively gather a large number of them from various websites to your hard drive every day. Soon, your hard drive accumulates tens of thousands of CG images with different sources and resolutions.

However, you discover that a significant portion of these downloaded CG images are duplicates, occupying precious disk space. Therefore, you urgently need a fast method to identify and remove duplicates from your CG directory. Since the sources of these downloads vary, traditional methods such as searching by file names are clearly inadequate for solving this problem; it's necessary to analyze the content of the images.

## Installation

Requirements:

1. [.NET 10 SDK](http://dot.net/)

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.NiBot
```

## Usage

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

With the `--interactive` option, it will preview each photo and ask for confirmation before deleting files.

## Install as a Class Library

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

## Run locally

Requirements about how to run

1. [.NET 10 SDK](http://dot.net/)
2. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.
