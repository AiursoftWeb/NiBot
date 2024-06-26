# NiBot

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/NiBot/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/NiBot/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/NiBot/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/NiBot/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/NiBot/-/pipelines)
[![NuGet version](https://img.shields.io/nuget/v/Aiursoft.NiBot.svg)](https://www.nuget.org/packages/Aiursoft.NiBot/)
[![ManHours](https://manhours.aiursoft.cn/r/gitlab.aiursoft.cn/aiursoft/nibot.svg)](https://gitlab.aiursoft.cn/aiursoft/nibot/-/commits/master?ref_type=heads)

A cli tool helps you to de-duplicate images in a folder.

Suppose you enjoy collecting images and intensively gather a large number of them from various websites to your hard drive every day. Soon, your hard drive accumulates tens of thousands of CG images with different sources and resolutions.

However, you discover that a significant portion of these downloaded CG images are duplicates, occupying precious disk space. Therefore, you urgently need a fast method to identify and remove duplicates from your CG directory. Since the sources of these downloads vary, traditional methods such as searching by file names are clearly inadequate for solving this problem; it's necessary to analyze the content of the images.

## Install

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.NiBot
```

## Usage

After getting the binary, run it directly in the terminal.

```bash
$ NiBot dedup
Option '--path' is required.

Description:
  De-duplicate images in a folder.

Usage:
  NiBot dedup [options]

Options:
  -p, --path <path> (REQUIRED)                  Path of the folder to dedup.
  -ds, --duplicate-similar <duplicate-similar>  Similarity bar. Default is [96]. This value means two image are considered as duplicates if their similarity is greater than it. Setting too small may cause different images
                                                to be considered as duplicates. Suggested values: [96-100] [default: 96]
  -r, --recursive                               Recursively search for similar images in subdirectories. Default is [false]. [default: False]
  -k, --keep <keep>                             Preference for sorting images by quality to determine which to keep when duplicates are found. Default is [Colorful,HighestResolution,Largest,Newest]. Available options:
                                                Newest, Oldest, Smallest, Largest, HighestResolution, LowestResolution. [default: Colorful|HighestResolution|Largest|Newest]
  -a, --action <Delete|MoveToTrash|Nothing>     Action to take when duplicates are found. Default is [Nothing]. Available options: Nothing, Delete, MoveToTrash, MoveAndCopyOriginalToTrash. [default: Nothing]
  -i, --interactive                             Interactive mode. Ask for confirmation before deleting files. Default is [false]. [default: False]
  -e, --extensions <extensions>                 Extensions of files to dedup. Default is [jpg,jpeg,png,jfif]. [default: jpg|jpeg|png|jfif]
  -v, --verbose                                 Show detailed log
  -?, -h, --help                                Show help and usage information
```

It will fetch all images in the folder and compare them with each other. If two images are similar enough, it will consider them as duplicates. 

It will pick the best one based the `--keep` option. If the `--action` is set to `Delete`, it will delete the rest of the duplicates. If the `--action` is set to `MoveToTrash`, it will move the rest of the duplicates to the trash.

With the `--interactive` option, it will preview each photo and ask for confirmation before deleting files.
