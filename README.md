# Manga RePacker

A simple tool for reorganizing manga chapters into volume-based `.cbz` archives.

## What It Does

Scans a directory for `.cbz` files containing manga chapters, parses volume and chapter information from embedded `ComicInfo.xml` metadata, and repackages them into organized volumes.

**Example:**
- **Before:** Multiple chapter files (e.g., `chapter1.cbz`, `chapter2.cbz`)
- **After:** Consolidated volume archives (e.g., `Vol 001.cbz`, `Vol 002.cbz`) with properly numbered chapters inside

## Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/albertakhmetov/manga-repacker.git
   cd manga-repacker
   ```

2. Build the project (requires .NET 10 SDK):
   ```bash
   dotnet build
   ```

## Usage

Run from the `target/Release/MangaRePacker` directory:

```bash
MangaRePacker.exe <source_directory> <output_directory>
```

Where:
- `source_directory`: Contains your `.cbz` chapter files
- `output_directory`: Where the new volume archives will be created

## Features

- **Volume-based reorganization**: Groups chapters by volume
- **Proper numbering**: Adds leading zeros for correct file sorting
- **Parallel processing**: Fast handling of multiple files
- **Error reporting**: Highlights missing files or parsing issues

## License

GPL-3.0
