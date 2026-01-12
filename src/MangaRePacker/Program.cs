/*  Copyright © 2026, Albert Akhmetov <akhmetov@live.com>   
 *
 *  This file is part of Manga RePacker.
 *
 *  Manga RePacker is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Manga RePacker is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Manga RePacker. If not, see <https://www.gnu.org/licenses/>.   
 *
 */

using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using MangaRePacker;

const double MAX_VOLUME_SIZE = 84 * 1024 * 1024;

try
{
    if (args.Length != 2)
    {
        Console.WriteLine("Usage: MangaRePacker.exe {source directory} {output directory}");

        return;
    }

    var sourceDirectory = args[0];
    var outputDirectory = args[1];

    Console.WriteLine($"Source: {sourceDirectory}");
    Console.WriteLine($"Output: {outputDirectory}");

    var data = ComicInfoParser.ParseDirectory(sourceDirectory);

    if (data.Values.Any(x => x is null))
    {
        Console.ForegroundColor = ConsoleColor.Red;

        foreach (var file in data.Where(x => x.Value is null).Select(x => Path.GetFileNameWithoutExtension(x.Key)))
        {
            Console.WriteLine($"Can't parse file: {file}");
        }

        Console.ResetColor();

        return;
    }

    if (!Directory.Exists(sourceDirectory))
    {
        throw new DirectoryNotFoundException($"Source Directory isn't found: {sourceDirectory}");
    }

    ReorganizeComics(sourceDirectory, outputDirectory, data);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;

    Console.WriteLine($"Error during packing: {ex}");

    Console.ResetColor();
}

static void ReorganizeComics(string sourceDirectory, string outputDirectory, ConcurrentDictionary<string, ComicInfo?> comicInfos)
{
    // Group by volume
    var volumes = comicInfos
        .GroupBy(kv => kv.Value!.Volume)
        .ToDictionary(g => g.Key, g => g.ToList());

    if (!volumes.Any()) return;

    var mangaName = Path.GetFileName(sourceDirectory);

    // Creating directory
    var targetDirectory = Path.Combine(outputDirectory, mangaName);
    if (Directory.Exists(targetDirectory))
    {
        throw new IOException($"Output Directory is exist: {targetDirectory}");
    }
    else
    {
        Directory.CreateDirectory(targetDirectory);
    }

    // Determine padding length
    var maxVolume = volumes.Keys.Max();
    var padding = maxVolume.ToString().Length;

    // Create volume archives
    foreach (var volumeGroup in volumes.OrderBy(volume => volume.Key))
    {
        var volumeNumber = volumeGroup.Key;
        var volumeName = $"Vol {volumeNumber.ToString().PadLeft(padding, '0')}";

        Console.Write($"Generating volume {volumeName}/{maxVolume}...");

        ZipArchive? archive = null;

        var originalSize = volumeGroup.Value.Select(x => new FileInfo(Path.Combine(sourceDirectory, x.Key)).Length).Sum();
        var recommendedVolumeSize = originalSize / Math.Round(originalSize / MAX_VOLUME_SIZE);

        try
        {
            var maxChapter = volumeGroup.Value.Max(x => x.Value!.Number);
            var chapterPadding = Math.Truncate(maxChapter).ToString().Length;

            var volumeSize = 0L;
            var subVolumeNumber = MAX_VOLUME_SIZE > 0 && originalSize > MAX_VOLUME_SIZE * 1.33 ? 1 : 0;

            foreach (var (fileName, info) in volumeGroup.Value)
            {
                if (info == null) continue;

                if (subVolumeNumber > 0 && volumeSize > recommendedVolumeSize)
                {
                    archive?.Dispose();
                    archive = null;

                    volumeSize = 0;
                    subVolumeNumber++;
                }

                if (archive is null)
                {
                    var archivePath = subVolumeNumber == 0
                        ? Path.Combine(targetDirectory, $"{mangaName} - {volumeName}.cbz")
                        : Path.Combine(targetDirectory, $"{mangaName} - {volumeName}.{subVolumeNumber}.cbz");

                    archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                }

                var sourcePath = Path.Combine(sourceDirectory, fileName);
                var folderName = FormatChapterNumber(info.Number, chapterPadding);

                using var sourceArchive = ZipFile.OpenRead(sourcePath);

                foreach (var entry in sourceArchive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string entryPath = Path.Combine(folderName, entry.Name);
                    var newEntry = archive.CreateEntry(entryPath);

                    using var sourceStream = entry.Open();
                    using var newStream = newEntry.Open();
                    sourceStream.CopyTo(newStream);

                    volumeSize += entry.CompressedLength;
                }
            }
        }
        finally
        {
            archive?.Dispose();
        }

        Console.WriteLine(" done.");
    }
}

static string FormatChapterNumber(double chapterNumber, int padding)
{
    string str = chapterNumber.ToString(CultureInfo.InvariantCulture);
    var parts = str.Split('.');

    string wholePart = parts[0].PadLeft(padding, '0');

    return parts.Length > 1 ? $"{wholePart}.{parts[1]}" : wholePart;
}