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
namespace MangaRePacker;

using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

public record ComicInfo(int Volume, double Number, string Title);

public static class ComicInfoParser
{
    public static ConcurrentDictionary<string, ComicInfo?> ParseDirectory(string directoryPath)
    {
        var result = new ConcurrentDictionary<string, ComicInfo?>();
        var cbzFiles = Directory.EnumerateFiles(directoryPath, "*.cbz");

        Parallel.ForEach(cbzFiles, cbzFile =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(cbzFile);
                var xmlEntry = archive.Entries.FirstOrDefault(e => 
                    e.Name.Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));
                    
                if (xmlEntry is null)
                {
                    result[Path.GetFileName(cbzFile)] = null;
                    return ;
                }

                using var stream = xmlEntry.Open();
                using var reader = XmlReader.Create(stream);
                
                string? title = null;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "Title")
                    {
                        title = reader.ReadElementContentAsString();
                        break;
                    }
                }

                if (title is not null)
                {
                    var info = ParseTitle(title);
                    result[Path.GetFileName(cbzFile)] = info;
                }
                else
                {
                    result[Path.GetFileName(cbzFile)] = null;
                }
            }
            catch
            {
                result[Path.GetFileName(cbzFile)] = null;
            }
        });

        return result;
    }

    private static ComicInfo? ParseTitle(string title)
    {
        try
        {
            if (title.StartsWith("Том", StringComparison.OrdinalIgnoreCase))
            {
                return ParseTomFormat(title);
            }
            else if (title.Contains(" - ", StringComparison.Ordinal))
            {
                return ParseDashFormat(title);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static ComicInfo? ParseTomFormat(string title)
    {
        var parts = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return null;

        int volume = int.Parse(parts[1].TrimEnd('.'));
        
        var numberMatch = System.Text.RegularExpressions.Regex.Match(parts[3], @"(\d+(?:\.\d+)?)");
        if (!numberMatch.Success) return null;
        double number = double.Parse(numberMatch.Value, System.Globalization.CultureInfo.InvariantCulture);

        var titleStart = title.IndexOf(" - ", StringComparison.Ordinal);
        string chapterTitle = titleStart > 0 ? title[(titleStart + 3)..] : string.Empty;

        return new ComicInfo(volume, number, chapterTitle);
    }

    private static ComicInfo? ParseDashFormat(string title)
    {
        // Patterns:
        // "1 - 1 Chapter Title"
        // "1 - 1.5 Chapter Title" 
        // "1 - 1 Chapter Title With - Dash"
        var pattern = @"(?<volume>\d+)\s*-\s*(?<number>\d+(?:\.\d+)?)(\s+(?<title>.+))?";
        var match = Regex.Match(title, pattern, RegexOptions.ExplicitCapture);

        if (match.Success && match.Groups.Count == 4)
        {
            int volume = int.Parse(match.Groups["volume"].Value);
            double number = double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            string chapterTitle = match.Groups["title"].Value.Trim();

            return new ComicInfo(volume, number, chapterTitle);
        }

        return null;
    }
}