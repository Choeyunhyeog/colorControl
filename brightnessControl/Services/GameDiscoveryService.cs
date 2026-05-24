using System.IO;
using System.Text.RegularExpressions;
using brightnessControl.Models;

namespace brightnessControl.Services;

public sealed partial class GameDiscoveryService
{
    private readonly AppLogger _logger;

    public GameDiscoveryService(AppLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<InstalledGame> DiscoverInstalledGames()
    {
        var games = new List<InstalledGame>();

        games.AddRange(DiscoverSteamGames());
        games.AddRange(DiscoverGamesInCommonFolders());

        var uniqueGames = games
            .Where(game => !string.IsNullOrWhiteSpace(game.ProcessName))
            .GroupBy(game => game.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _logger.Info($"Discovered {uniqueGames.Count} installed game candidate(s).");
        return uniqueGames;
    }

    private IEnumerable<InstalledGame> DiscoverSteamGames()
    {
        foreach (var libraryPath in GetSteamLibraryPaths())
        {
            var steamAppsPath = Path.Combine(libraryPath, "steamapps");
            if (!Directory.Exists(steamAppsPath))
            {
                continue;
            }

            foreach (var manifestPath in EnumerateFilesSafe(steamAppsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
            {
                var manifest = ReadTextSafe(manifestPath);
                if (manifest is null)
                {
                    continue;
                }

                var name = ReadValveKeyValue(manifest, "name");
                var installDir = ReadValveKeyValue(manifest, "installdir");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDir))
                {
                    continue;
                }

                var gamePath = Path.Combine(steamAppsPath, "common", installDir);
                var executablePath = FindBestExecutable(gamePath, name, installDir);
                if (executablePath is null)
                {
                    continue;
                }

                yield return new InstalledGame
                {
                    Name = name,
                    ProcessName = Path.GetFileName(executablePath),
                    ExecutablePath = executablePath
                };
            }
        }
    }

    private IEnumerable<InstalledGame> DiscoverGamesInCommonFolders()
    {
        foreach (var folder in GetCommonGameFolders())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var gameFolder in EnumerateDirectoriesSafe(folder))
            {
                var name = Path.GetFileName(gameFolder);
                var executablePath = FindBestExecutable(gameFolder, name, name);
                if (executablePath is null)
                {
                    continue;
                }

                yield return new InstalledGame
                {
                    Name = name,
                    ProcessName = Path.GetFileName(executablePath),
                    ExecutablePath = executablePath
                };
            }
        }
    }

    private static IEnumerable<string> GetSteamLibraryPaths()
    {
        var steamRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        foreach (var steamRoot in steamRoots.Where(Directory.Exists))
        {
            yield return steamRoot;

            var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            var libraryFolders = ReadTextSafe(libraryFoldersPath);
            if (libraryFolders is null)
            {
                continue;
            }

            foreach (Match match in ValvePathRegex().Matches(libraryFolders))
            {
                var path = match.Groups["value"].Value.Replace(@"\\", @"\");
                if (Directory.Exists(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> GetCommonGameFolders()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return
        [
            @"C:\XboxGames",
            @"C:\Games",
            @"C:\GOG Games",
            Path.Combine(programFiles, "Epic Games"),
            Path.Combine(programFilesX86, "Epic Games"),
            Path.Combine(programFiles, "GOG Galaxy", "Games"),
            Path.Combine(programFilesX86, "GOG Galaxy", "Games"),
            Path.Combine(programFiles, "Riot Games"),
            Path.Combine(programFilesX86, "Riot Games")
        ];
    }

    private static string? FindBestExecutable(string gamePath, string gameName, string installDir)
    {
        if (!Directory.Exists(gamePath))
        {
            return null;
        }

        var executables = EnumerateFilesSafe(gamePath, "*.exe", SearchOption.AllDirectories)
            .Where(IsLikelyGameExecutable)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists)
            .OrderByDescending(file => GetExecutableScore(file, gamePath, gameName, installDir))
            .ThenByDescending(file => file.Length)
            .Take(1)
            .Select(file => file.FullName);

        return executables.FirstOrDefault();
    }

    private static bool IsLikelyGameExecutable(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var lowerPath = path.ToLowerInvariant();

        if (fileName.Contains("unins", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("crash", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("report", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("install", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("redist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !lowerPath.Contains(@"\_commonredist\") &&
               !lowerPath.Contains(@"\directx\") &&
               !lowerPath.Contains(@"\dotnet\") &&
               !lowerPath.Contains(@"\support\") &&
               !lowerPath.Contains(@"\installer\") &&
               !lowerPath.Contains(@"\vcredist\");
    }

    private static int GetExecutableScore(FileInfo file, string gamePath, string gameName, string installDir)
    {
        var score = 0;
        var fileName = Path.GetFileNameWithoutExtension(file.Name);
        var normalizedFileName = NormalizeName(fileName);
        var normalizedGameName = NormalizeName(gameName);
        var normalizedInstallDir = NormalizeName(installDir);

        if (string.Equals(file.DirectoryName, gamePath, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (normalizedFileName == normalizedGameName || normalizedFileName == normalizedInstallDir)
        {
            score += 100;
        }
        else if (normalizedGameName.Contains(normalizedFileName, StringComparison.OrdinalIgnoreCase) ||
                 normalizedFileName.Contains(normalizedInstallDir, StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
        }

        if (fileName.Contains("launcher", StringComparison.OrdinalIgnoreCase))
        {
            score -= 30;
        }

        return score;
    }

    private static string NormalizeName(string value)
    {
        return NameCleanupRegex().Replace(value, string.Empty).ToLowerInvariant();
    }

    private static string? ReadValveKeyValue(string text, string key)
    {
        var match = Regex.Match(text, $"""
            "{Regex.Escape(key)}"\s+"(?<value>[^"]+)"
            """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ReadTextSafe(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(
        string path,
        string searchPattern,
        SearchOption searchOption)
    {
        if (searchOption == SearchOption.AllDirectories)
        {
            return EnumerateFilesRecursiveSafe(path, searchPattern, maxDepth: 6);
        }

        try
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateFilesRecursiveSafe(
        string rootPath,
        string searchPattern,
        int maxDepth)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((rootPath, 0));

        while (pending.Count > 0)
        {
            var (currentPath, depth) = pending.Dequeue();

            foreach (var file in EnumerateFilesSafe(currentPath, searchPattern, SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var directory in EnumerateDirectoriesSafe(currentPath))
            {
                pending.Enqueue((directory, depth + 1));
            }
        }
    }

    [GeneratedRegex(@"""path""\s+""(?<value>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ValvePathRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex NameCleanupRegex();
}
