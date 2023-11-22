# NiBot

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/Ni-Bot/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/Ni-Bot/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/Ni-Bot/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/Ni-Bot/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/Ni-Bot/-/pipelines)
[![NuGet version](https://img.shields.io/nuget/v/Aiursoft.NiBot.svg)](https://www.nuget.org/packages/Aiursoft.NiBot/)
[![ManHours](https://manhours.aiursoft.cn/gitlab/gitlab.aiursoft.cn/aiursoft/ni-bot)](https://gitlab.aiursoft.cn/aiursoft/ni-bot/-/commits/master?ref_type=heads)

A small project helps me to parse and save my videos.

## Install

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.NiBot
```

## Usage

After getting the binary, run it directly in the terminal.

```bash
$ NiBot
Required command was not provided.
Option '--path' is required.

Description:
  A cli tool project helps to re-encode and save all videos under a path.

Usage:
  NiBot [command] [options]

Options:
  -p, --path <path> (REQUIRED)  Path of the program to run.
  -d, --dry-run                 Preview changes without actually making them
  -v, --verbose                 Show detailed log
  --version                     Show version information
  -?, -h, --help                Show help and usage information

Commands:
  some-logic  The command to do something.
```

It will fetch all videos under that folder, and try to re-encode it with ffmpeg.
