using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

class Program
{
    static int progressCounter = 0;
    static readonly object progressLock = new();
    static DateTime startTime;
    static DateTime? lastUpdateTime = null;

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            new Argument<string>(
                "keyword",
                description: "The keyword to search for"),
            new Option<bool>(
                ["-c", "--case-sensitive"],
                description: "Make the search case sensitive"),
            new Option<string>(
                ["-d", "--directory"],
                getDefaultValue: () => ".",
                description: "The directory to search in"),
            new Option<string>(
                ["-p", "--pattern"],
                getDefaultValue: () => "*",
                description: "The file pattern to search for (e.g., \"*.cs\", \"*.*\")"),
            new Option<bool>(
                ["-s", "--smart-search"],
                getDefaultValue: () => true,
                description: "Enable smart search to ignore common directories and binary files")
        };

        rootCommand.Description = "Search for a keyword in files in a directory";

        rootCommand.Handler = CommandHandler.Create<string, bool, string, string, bool>(SearchAndDisplayResults);

        return await rootCommand.InvokeAsync(args);
    }

    static void SearchAndDisplayResults(string keyword, bool caseSensitive, string directory, string pattern, bool smartSearch)
    {
        List<(string FilePath, int LineNumber, string Snippet)> results = SearchFiles(keyword, caseSensitive, directory, pattern, smartSearch);
        int numResults = results.Count;

        Console.WriteLine();

        if (numResults > 100)
        {
            Console.Write($"Found {numResults} instances. Do you want to print all results? (y/n): ");
            string? userInput = Console.ReadLine();
            if (userInput != null && !userInput.Equals("y", StringComparison.CurrentCultureIgnoreCase))
            {
                Console.WriteLine($"{numResults} instances found.");
                Console.WriteLine($"Completed in {(DateTime.Now - startTime).TotalSeconds:F2} seconds");
                return;
            }
        }

        foreach (var (FilePath, LineNumber, Snippet) in results)
        {
            Console.Write($"{FilePath.Replace(directory, "")}, line {LineNumber}: ");
            WriteHighlighted(Snippet, keyword, ConsoleColor.DarkGray, caseSensitive);
        }

        Console.WriteLine($"Found {numResults} instances in {(DateTime.Now - startTime).TotalSeconds:F2} seconds");
    }

    static List<(string FilePath, int LineNumber, string Snippet)> SearchFiles(string keyword, bool caseSensitive, string directory, string filePattern, bool smartSearch)
    {
        List<(string FilePath, int LineNumber, string Snippet)> allResults = [];
        List<string> files = [.. Directory.GetFiles(directory, filePattern, SearchOption.AllDirectories)];

        if (smartSearch)
        {
            files = files.Where(file => !ShouldIgnoreDirectory(file) && !IsBinaryFile(file)).ToList();
        }

        int totalFiles = files.Count;
        startTime = DateTime.Now;

        Parallel.ForEach(files, file =>
        {
            List<(string FilePath, int LineNumber, string Snippet)> results = SearchFile(keyword, caseSensitive, file);
            lock (progressLock)
            {
                progressCounter++;
                DateTime currentTime = DateTime.Now;
                if (!lastUpdateTime.HasValue || (currentTime - lastUpdateTime.Value).TotalSeconds >= 1)
                {
                    double percentage = (double)progressCounter / totalFiles * 100;
                    Console.Write($"\rProgress: {progressCounter}/{totalFiles} ({(int)percentage}%)");
                    lastUpdateTime = currentTime;
                }
                allResults.AddRange(results);
            }
        });

        return allResults;
    }

    static List<(string FilePath, int LineNumber, string Snippet)> SearchFile(string keyword, bool caseSensitive, string filePath)
    {
        List<(string FilePath, int LineNumber, string Snippet)> results = new List<(string FilePath, int LineNumber, string Snippet)>();

        try
        {
            using StreamReader reader = new StreamReader(filePath);
            string line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                int index = caseSensitive ? line.IndexOf(keyword) : line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    int start = Math.Max(index - 20, 0);
                    int end = Math.Min(index + keyword.Length + 20, line.Length);

                    // Adjust start to the nearest whole word
                    while (start > 0 && !char.IsWhiteSpace(line[start - 1]))
                    {
                        start--;
                    }

                    // Adjust end to the nearest whole word
                    while (end < line.Length && !char.IsWhiteSpace(line[end]))
                    {
                        end++;
                    }

                    // Adjust start to ensure it does not break the word
                    if (start > 0 && !char.IsWhiteSpace(line[start]))
                    {
                        int tempStart = start;
                        while (tempStart < index && !char.IsWhiteSpace(line[tempStart]))
                        {
                            tempStart++;
                        }
                        if (tempStart - start <= 20)
                        {
                            start = tempStart;
                        }
                    }

                    // Adjust end to ensure it does not break the word
                    if (end < line.Length && !char.IsWhiteSpace(line[end - 1]))
                    {
                        int tempEnd = end;
                        while (tempEnd > index + keyword.Length && !char.IsWhiteSpace(line[tempEnd - 1]))
                        {
                            tempEnd--;
                        }
                        if (end - tempEnd <= 20)
                        {
                            end = tempEnd;
                        }
                    }

                    string snippet = line[start..end].Trim();
                    results.Add((filePath, lineNumber, snippet));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading file {filePath}: {ex.Message}");
        }

        return results;
    }

    static readonly string[] ignoreDirs = [ ".git", "bin", "obj", "__pycache__", "node_modules", ".vs", ".vscode", ".idea" ];
    static bool ShouldIgnoreDirectory(string path)
    {
        return ignoreDirs.Any(path.Contains);
    }

    static readonly HashSet<string> ignoreExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".so", ".bin", ".img", ".iso",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".ico",
        ".pdf", ".zip", ".tar", ".gz", ".7z", ".rar",
        ".mp3", ".wav", ".flac", ".mp4", ".mkv", ".avi", ".mov"
    };
    
    static bool IsBinaryFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ignoreExtensions.Contains(ext);
    }

    static void WriteHighlighted(string text, string keyword, ConsoleColor highlightColor, bool caseSensitive)
    {
        int index = 0;
        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        while ((index = text.IndexOf(keyword, index, comparison)) != -1)
        {
            // Write text before the keyword
            Console.Write(text.Substring(0, index));
            
            // Set the highlight color and write the keyword
            Console.ForegroundColor = highlightColor;
            Console.Write(text.Substring(index, keyword.Length));
            Console.ResetColor();
            
            // Move the starting point past the keyword
            text = text.Substring(index + keyword.Length);
            index = 0;
        }

        // Write the rest of the text
        Console.WriteLine(text);
    }
}
