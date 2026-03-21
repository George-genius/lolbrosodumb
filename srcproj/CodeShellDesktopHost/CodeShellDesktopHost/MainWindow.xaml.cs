using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CodeShellDesktopHost.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace CodeShellDesktopHost;

public partial class MainWindow : Window
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SqliteStorageService _db;
    private readonly string _appDataDir;
    private readonly string _dbPath;
    private readonly string _webViewUserData;
    private readonly string _wwwroot;
    private readonly string _homeDir;

    public MainWindow()
    {
        InitializeComponent();

        _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeShellDesktopHost");
        _dbPath = Path.Combine(_appDataDir, "appdata.db");
        _webViewUserData = Path.Combine(_appDataDir, "WebView2");
        _wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        _homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Directory.CreateDirectory(_appDataDir);
        Directory.CreateDirectory(_webViewUserData);
        Directory.CreateDirectory(_wwwroot);

        _db = new SqliteStorageService(_dbPath);
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _db.InitializeAsync();

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _webViewUserData);
            await Browser.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 could not start. Install the Microsoft Edge WebView2 Runtime, then relaunch.\n\n" + ex.Message,
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
            return;
        }

        Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
        Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        Browser.CoreWebView2.Settings.IsStatusBarEnabled = true;
        Browser.CoreWebView2.Settings.IsZoomControlEnabled = true;

        Browser.CoreWebView2.WebMessageReceived += Browser_WebMessageReceived;
        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping("app", _wwwroot, CoreWebView2HostResourceAccessKind.Allow);

        var bridgePath = Path.Combine(_wwwroot, "bridge.js");
        if (File.Exists(bridgePath))
        {
            await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(await File.ReadAllTextAsync(bridgePath));
        }

        var entry = Path.Combine(_wwwroot, "index.html");
        if (!File.Exists(entry))
        {
            await File.WriteAllTextAsync(entry, "<html><body><h1>Missing wwwroot/index.html</h1></body></html>");
        }

        Browser.Source = new Uri("https://app/index.html");
    }

    private async void Browser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        BridgeRequest? req = null;
        try
        {
            req = JsonSerializer.Deserialize<BridgeRequest>(e.WebMessageAsJson, _jsonOptions);
            if (req is null || string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Method)) return;
            var result = await HandleRequestAsync(req.Method, req.Args);
            PostBridgeResponse(req.Id, true, result, null);
        }
        catch (Exception ex)
        {
            PostBridgeResponse(req?.Id ?? "unknown", false, null, ex.Message);
        }
    }

    private async Task<object?> HandleRequestAsync(string method, JsonElement args)
    {
        return method switch
        {
            "app.getInfo" => new
            {
                appName = "CodeShellDesktopHost",
                dbPath = _dbPath,
                appDataDir = _appDataDir,
                runtime = Environment.Version.ToString(),
                os = Environment.OSVersion.VersionString,
                homeDir = _homeDir
            },
            "localStore.getItem" => new { value = await _db.GetItemAsync(GetRequiredString(args, "key")) },
            "localStore.setItem" => new { ok = await _db.SetItemAsync(GetRequiredString(args, "key"), GetString(args, "value") ?? string.Empty) },
            "localStore.removeItem" => new { ok = await _db.RemoveItemAsync(GetRequiredString(args, "key")) },
            "localStore.clear" => new { ok = await _db.ClearKvAsync() },
            "localStore.keys" => new { keys = await _db.GetKeysAsync() },
            "blobStore.putBase64" => await PutBlobAsync(args),
            "blobStore.getBase64" => await GetBlobAsync(args),
            "blobStore.remove" => new { ok = await _db.RemoveBlobAsync(GetRequiredString(args, "key")) },
            "blobStore.list" => new { items = await _db.ListBlobsAsync() },
            "settings.get" => new { value = await _db.GetSettingAsync(GetRequiredString(args, "key")) ?? GetString(args, "fallback") },
            "settings.set" => new { ok = await _db.SetSettingAsync(GetRequiredString(args, "key"), GetString(args, "value") ?? string.Empty) },
            "dialog.pickFile" => await PickFileAsync(args, false),
            "dialog.pickFiles" => await PickFileAsync(args, true),
            "dialog.pickFolder" => PickFolder(args),
            "download.saveBase64" => await SaveBase64Async(args),
            "download.saveText" => await SaveTextAsync(args),
            "download.saveJson" => await SaveJsonAsync(args),
            "fs.getContext" => GetFsContext(),
            "fs.getRoots" => GetFsRoots(),
            "fs.changeDirectory" => ChangeDirectory(args),
            "fs.list" => ListPath(args),
            "fs.stat" => StatPath(args),
            "fs.readText" => await ReadTextFileAsync(args),
            "fs.writeText" => await WriteTextFileAsync(args),
            "fs.readBase64" => await ReadBase64FileAsync(args),
            "fs.writeBase64" => await WriteBase64FileAsync(args),
            "fs.createDirectory" => CreateDirectory(args),
            "fs.createEmptyFile" => CreateEmptyFile(args),
            "fs.deletePath" => DeletePath(args),
            "fs.copyPath" => CopyPath(args),
            "fs.movePath" => MovePath(args),
            "fs.find" => FindPaths(args),
            "fs.grep" => await GrepFileAsync(args),
            "fs.directorySummary" => DirectorySummary(args),
            "fs.largestFiles" => LargestFiles(args),
            _ => throw new InvalidOperationException($"Unknown bridge method: {method}")
        };
    }

    private object GetFsContext() => new
    {
        cwd = _homeDir,
        home = _homeDir,
        desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        downloads = Path.Combine(_homeDir, "Downloads"),
        temp = Path.GetTempPath(),
        appData = _appDataDir,
        userName = Environment.UserName,
        machineName = Environment.MachineName
    };

    private object GetFsRoots()
    {
        var roots = DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => new
        {
            name = d.Name,
            path = d.RootDirectory.FullName,
            type = d.DriveType.ToString(),
            totalSize = SafeLong(() => d.TotalSize),
            availableFreeSpace = SafeLong(() => d.AvailableFreeSpace)
        }).ToList();
        return new { roots };
    }

    private object ChangeDirectory(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");
        return new { path = NormalizeForUi(path), home = _homeDir };
    }

    private object ListPath(JsonElement args)
    {
        var path = ResolvePath(GetString(args, "path") ?? ".", GetString(args, "cwd"));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");
        var di = new DirectoryInfo(path);
        var entries = EnumerateEntriesSafe(di.FullName).OrderBy(x => x is FileInfo ? 1 : 0).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(ToFsEntry).ToList();
        return new { path = NormalizeForUi(di.FullName), entries };
    }

    private object StatPath(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        var allowMissing = GetBool(args, "allowMissing", false);
        if (File.Exists(path)) return ToFsStat(new FileInfo(path));
        if (Directory.Exists(path)) return ToFsStat(new DirectoryInfo(path));
        if (allowMissing) return new { exists = false, path = NormalizeForUi(path), type = "missing", size = 0L };
        throw new FileNotFoundException($"Path not found: {path}");
    }

    private async Task<object> ReadTextFileAsync(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        if (!File.Exists(path)) throw new FileNotFoundException($"File not found: {path}");
        var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return new { path = NormalizeForUi(path), text, fileName = Path.GetFileName(path) };
    }

    private async Task<object> WriteTextFileAsync(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        EnsureParentDirectory(path);
        var text = GetString(args, "text") ?? string.Empty;
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);
        return new { ok = true, path = NormalizeForUi(path), size = new FileInfo(path).Length };
    }

    private async Task<object> ReadBase64FileAsync(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        if (!File.Exists(path)) throw new FileNotFoundException($"File not found: {path}");
        var bytes = await File.ReadAllBytesAsync(path);
        return new { path = NormalizeForUi(path), fileName = Path.GetFileName(path), base64 = Convert.ToBase64String(bytes), mimeType = GuessMimeType(Path.GetExtension(path)), size = bytes.LongLength };
    }

    private async Task<object> WriteBase64FileAsync(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        EnsureParentDirectory(path);
        var bytes = Convert.FromBase64String(GetRequiredString(args, "base64"));
        await File.WriteAllBytesAsync(path, bytes);
        return new { ok = true, path = NormalizeForUi(path), size = bytes.LongLength };
    }

    private object CreateDirectory(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        Directory.CreateDirectory(path);
        return new { ok = true, path = NormalizeForUi(path) };
    }

    private object CreateEmptyFile(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        EnsureParentDirectory(path);
        if (!File.Exists(path)) using (File.Create(path)) { }
        else File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        return new { ok = true, path = NormalizeForUi(path) };
    }

    private object DeletePath(JsonElement args)
    {
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        var recursive = GetBool(args, "recursive", false);
        if (File.Exists(path)) File.Delete(path);
        else if (Directory.Exists(path)) Directory.Delete(path, recursive);
        else throw new FileNotFoundException($"Path not found: {path}");
        return new { ok = true, path = NormalizeForUi(path) };
    }

    private object CopyPath(JsonElement args)
    {
        var src = ResolvePath(GetRequiredString(args, "sourcePath"), GetString(args, "cwd"));
        var dst = ResolvePath(GetRequiredString(args, "destinationPath"), GetString(args, "cwd"));
        if (File.Exists(src))
        {
            var finalDst = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(src)) : dst;
            EnsureParentDirectory(finalDst);
            File.Copy(src, finalDst, true);
            return new { ok = true, path = NormalizeForUi(finalDst) };
        }
        if (Directory.Exists(src))
        {
            var srcInfo = new DirectoryInfo(src);
            var finalDst = Directory.Exists(dst) ? Path.Combine(dst, srcInfo.Name) : dst;
            CopyDirectory(srcInfo.FullName, finalDst);
            return new { ok = true, path = NormalizeForUi(finalDst) };
        }
        throw new FileNotFoundException($"Path not found: {src}");
    }

    private object MovePath(JsonElement args)
    {
        var src = ResolvePath(GetRequiredString(args, "sourcePath"), GetString(args, "cwd"));
        var dst = ResolvePath(GetRequiredString(args, "destinationPath"), GetString(args, "cwd"));
        if (File.Exists(src))
        {
            var finalDst = Directory.Exists(dst) ? Path.Combine(dst, Path.GetFileName(src)) : dst;
            EnsureParentDirectory(finalDst);
            if (File.Exists(finalDst)) File.Delete(finalDst);
            File.Move(src, finalDst);
            return new { ok = true, path = NormalizeForUi(finalDst) };
        }
        if (Directory.Exists(src))
        {
            var srcInfo = new DirectoryInfo(src);
            var finalDst = Directory.Exists(dst) ? Path.Combine(dst, srcInfo.Name) : dst;
            if (Directory.Exists(finalDst)) Directory.Delete(finalDst, true);
            Directory.Move(src, finalDst);
            return new { ok = true, path = NormalizeForUi(finalDst) };
        }
        throw new FileNotFoundException($"Path not found: {src}");
    }

    private object FindPaths(JsonElement args)
    {
        var pattern = GetRequiredString(args, "pattern");
        var root = ResolvePath(".", GetString(args, "cwd"));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Directory not found: {root}");
        var items = new List<object>();
        foreach (var entry in EnumerateFileSystemEntriesSafe(root))
        {
            var name = Path.GetFileName(entry);
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new { name, path = NormalizeForUi(entry), type = Directory.Exists(entry) ? "dir" : "file" });
            }
        }
        return new { root = NormalizeForUi(root), items };
    }

    private async Task<object> GrepFileAsync(JsonElement args)
    {
        var pattern = GetRequiredString(args, "pattern");
        var path = ResolvePath(GetRequiredString(args, "path"), GetString(args, "cwd"));
        if (!File.Exists(path)) throw new FileNotFoundException($"File not found: {path}");
        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
        var matches = lines.Select((line, idx) => new { line, lineNumber = idx + 1 }).Where(x => x.line.Contains(pattern, StringComparison.OrdinalIgnoreCase)).Select(x => new { x.lineNumber, x.line }).ToList();
        return new { path = NormalizeForUi(path), matches };
    }

    private object DirectorySummary(JsonElement args)
    {
        var path = ResolvePath(GetString(args, "path") ?? ".", GetString(args, "cwd"));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");
        long totalSize = 0, fileCount = 0, dirCount = 0;
        foreach (var dir in EnumerateDirectoriesSafe(path)) dirCount++;
        foreach (var file in EnumerateFilesSafe(path))
        {
            fileCount++;
            totalSize += SafeLong(() => new FileInfo(file).Length);
        }
        return new { path = NormalizeForUi(path), totalSize, fileCount, dirCount };
    }

    private object LargestFiles(JsonElement args)
    {
        var path = ResolvePath(GetString(args, "path") ?? ".", GetString(args, "cwd"));
        var limit = GetInt(args, "limit", 12);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Directory not found: {path}");
        var items = EnumerateFilesSafe(path).Select(p => new FileInfo(p)).OrderByDescending(fi => SafeLong(() => fi.Length)).Take(limit).Select(fi => new { path = NormalizeForUi(fi.FullName), name = fi.Name, size = SafeLong(() => fi.Length) }).ToList();
        return new { path = NormalizeForUi(path), items };
    }

    private static void EnsureParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
    }

    private static IEnumerable<FileSystemInfo> EnumerateEntriesSafe(string path)
    {
        try
        {
            return new DirectoryInfo(path).EnumerateFileSystemInfos();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<FileSystemInfo>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<FileSystemInfo>();
        }
        catch (IOException)
        {
            return Array.Empty<FileSystemInfo>();
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var dir in EnumerateDirectoriesInDirectorySafe(current))
            {
                yield return dir;
                pending.Push(dir);
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in EnumerateFilesInDirectorySafe(current)) yield return file;
            foreach (var dir in EnumerateDirectoriesInDirectorySafe(current)) pending.Push(dir);
        }
    }

    private static IEnumerable<string> EnumerateFileSystemEntriesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in EnumerateEntriesInDirectorySafe(current))
            {
                yield return entry;
                if (Directory.Exists(entry)) pending.Push(entry);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesInDirectorySafe(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateFilesInDirectorySafe(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateEntriesInDirectorySafe(string path)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(path);
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir)) File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir)) CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
    }

    private object ToFsEntry(FileSystemInfo fsi)
    {
        var isDir = fsi.Attributes.HasFlag(FileAttributes.Directory);
        var size = isDir ? 0L : ((FileInfo)fsi).Length;
        return new
        {
            name = fsi.Name,
            path = NormalizeForUi(fsi.FullName),
            type = isDir ? "dir" : "file",
            size,
            createdUtc = fsi.CreationTimeUtc.ToString("O"),
            modifiedUtc = fsi.LastWriteTimeUtc.ToString("O"),
            createdLocal = fsi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            modifiedLocal = fsi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            isHidden = fsi.Attributes.HasFlag(FileAttributes.Hidden),
            isReadOnly = fsi.Attributes.HasFlag(FileAttributes.ReadOnly)
        };
    }

    private object ToFsStat(FileSystemInfo fsi)
    {
        var isDir = fsi.Attributes.HasFlag(FileAttributes.Directory);
        var size = isDir ? 0L : ((FileInfo)fsi).Length;
        return new
        {
            exists = true,
            name = fsi.Name,
            path = NormalizeForUi(fsi.FullName),
            type = isDir ? "dir" : "file",
            size,
            createdUtc = fsi.CreationTimeUtc.ToString("O"),
            modifiedUtc = fsi.LastWriteTimeUtc.ToString("O"),
            createdLocal = fsi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            modifiedLocal = fsi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            isHidden = fsi.Attributes.HasFlag(FileAttributes.Hidden),
            isReadOnly = fsi.Attributes.HasFlag(FileAttributes.ReadOnly)
        };
    }

    private async Task<object> PutBlobAsync(JsonElement args)
    {
        var key = GetRequiredString(args, "key");
        var fileName = GetString(args, "fileName");
        var mimeType = GetString(args, "mimeType") ?? "application/octet-stream";
        var bytes = Convert.FromBase64String(GetRequiredString(args, "base64"));
        await _db.PutBlobAsync(key, fileName, mimeType, bytes);
        return new { ok = true, key, size = bytes.LongLength, mimeType, fileName };
    }

    private async Task<object?> GetBlobAsync(JsonElement args)
    {
        var item = await _db.GetBlobAsync(GetRequiredString(args, "key"));
        if (item is null) return null;
        return new { key = item.Key, fileName = item.FileName, mimeType = item.MimeType, base64 = Convert.ToBase64String(item.Data), size = item.Data.LongLength, updatedUtc = item.UpdatedUtc };
    }

    private async Task<object> PickFileAsync(JsonElement args, bool many)
    {
        var includeBase64 = GetBool(args, "includeBase64", true);
        var includeText = GetBool(args, "includeText", false);
        var title = GetString(args, "title") ?? (many ? "Choose files" : "Choose a file");
        var filter = BuildDialogFilter(GetString(args, "accept"));
        if (many)
        {
            var dlg = new OpenFileDialog { Title = title, Filter = filter, Multiselect = true };
            if (dlg.ShowDialog(this) != true) return new { cancelled = true, files = Array.Empty<object>() };
            var files = new List<object>();
            foreach (var path in dlg.FileNames) files.Add(await DescribeFileAsync(path, includeBase64, includeText));
            return new { cancelled = false, files };
        }
        else
        {
            var dlg = new OpenFileDialog { Title = title, Filter = filter, Multiselect = false };
            if (dlg.ShowDialog(this) != true) return new { cancelled = true, file = (object?)null };
            return new { cancelled = false, file = await DescribeFileAsync(dlg.FileName, includeBase64, includeText) };
        }
    }

    private object PickFolder(JsonElement args)
    {
        var title = GetString(args, "title") ?? "Choose a folder";
        var dlg = new OpenFolderDialog { Title = title, Multiselect = false };
        if (dlg.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dlg.FolderName)) return new { cancelled = true };
        return new { cancelled = false, path = NormalizeForUi(dlg.FolderName), name = new DirectoryInfo(dlg.FolderName).Name };
    }

    private async Task<object> DescribeFileAsync(string path, bool includeBase64, bool includeText)
    {
        var fi = new FileInfo(path);
        string? base64 = includeBase64 ? Convert.ToBase64String(await File.ReadAllBytesAsync(path)) : null;
        string? text = includeText ? await File.ReadAllTextAsync(path, Encoding.UTF8) : null;
        return new { name = fi.Name, path = NormalizeForUi(fi.FullName), size = fi.Length, mimeType = GuessMimeType(fi.Extension), base64, text };
    }

    private async Task<object> SaveBase64Async(JsonElement args)
    {
        var fileName = GetString(args, "fileName") ?? "download.bin";
        var dlg = new SaveFileDialog { FileName = fileName, Filter = BuildSaveDialogFilter(GetString(args, "filter"), fileName), DefaultExt = GetString(args, "defaultExtension") ?? Path.GetExtension(fileName) };
        if (dlg.ShowDialog(this) != true) return new { cancelled = true };
        var data = Convert.FromBase64String(GetRequiredString(args, "base64"));
        await File.WriteAllBytesAsync(dlg.FileName, data);
        return new { cancelled = false, path = NormalizeForUi(dlg.FileName), size = data.LongLength };
    }

    private async Task<object> SaveTextAsync(JsonElement args)
    {
        var fileName = GetString(args, "fileName") ?? "download.txt";
        var text = GetString(args, "text") ?? string.Empty;
        var enc = Encoding.GetEncoding(GetString(args, "encoding") ?? "utf-8");
        var dlg = new SaveFileDialog { FileName = fileName, Filter = BuildSaveDialogFilter(GetString(args, "filter"), fileName), DefaultExt = GetString(args, "defaultExtension") ?? Path.GetExtension(fileName) };
        if (dlg.ShowDialog(this) != true) return new { cancelled = true };
        await File.WriteAllTextAsync(dlg.FileName, text, enc);
        return new { cancelled = false, path = NormalizeForUi(dlg.FileName), length = text.Length };
    }

    private async Task<object> SaveJsonAsync(JsonElement args)
    {
        var fileName = GetString(args, "fileName") ?? "download.json";
        string text = args.TryGetProperty("value", out var valueElement)
            ? JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(valueElement.GetRawText()), new JsonSerializerOptions { WriteIndented = true })
            : GetString(args, "json") ?? "{}";
        var dlg = new SaveFileDialog { FileName = fileName, Filter = BuildSaveDialogFilter(GetString(args, "filter") ?? "JSON file|*.json|All files|*.*", fileName), DefaultExt = ".json" };
        if (dlg.ShowDialog(this) != true) return new { cancelled = true };
        await File.WriteAllTextAsync(dlg.FileName, text, Encoding.UTF8);
        return new { cancelled = false, path = NormalizeForUi(dlg.FileName), length = text.Length };
    }

    private void PostBridgeResponse(string id, bool ok, object? result, string? error)
    {
        if (Browser.CoreWebView2 is null) return;
        var json = JsonSerializer.Serialize(new { kind = "bridge-response", id, ok, result, error }, _jsonOptions);
        Browser.CoreWebView2.PostWebMessageAsJson(json);
    }

    private static string GetRequiredString(JsonElement args, string name)
    {
        var value = GetString(args, name);
        return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"Missing required argument: {name}");
    }

    private static string? GetString(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.GetRawText()
        };
    }

    private static bool GetBool(JsonElement args, string name, bool fallback)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static int GetInt(JsonElement args, string name, int fallback)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsedNumber)) return parsedNumber;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsedString)) return parsedString;
        return fallback;
    }

    private string ResolvePath(string path, string? cwd)
    {
        var trimmed = path.Trim().Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == ".") return Path.GetFullPath(string.IsNullOrWhiteSpace(cwd) ? _homeDir : cwd);
        if (trimmed == "~") return _homeDir;
        if (trimmed.StartsWith("~" + Path.DirectorySeparatorChar)) return Path.GetFullPath(Path.Combine(_homeDir, trimmed[2..]));
        if (Path.IsPathRooted(trimmed)) return Path.GetFullPath(trimmed);
        return Path.GetFullPath(Path.Combine(string.IsNullOrWhiteSpace(cwd) ? _homeDir : cwd, trimmed));
    }

    private static string NormalizeForUi(string path) => Path.GetFullPath(path).Replace('/', '\\');

    private static string BuildDialogFilter(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept)) return "All files|*.*";
        var patterns = AcceptToPatterns(accept);
        return patterns.Count == 0 ? "All files|*.*" : $"Accepted files|{string.Join(";", patterns)}|All files|*.*";
    }

    private static string BuildSaveDialogFilter(string? filter, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(filter) && filter.Contains('|')) return filter;
        var ext = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(ext) ? "All files|*.*" : $"{ext.TrimStart('.').ToUpperInvariant()} files|*{ext}|All files|*.*";
    }

    private static List<string> AcceptToPatterns(string accept)
    {
        var parts = accept.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var patterns = new List<string>();
        foreach (var part in parts)
        {
            if (part.StartsWith('.')) patterns.Add($"*{part}");
            else if (part.Equals("image/*", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp", "*.svg"]);
            else if (part.Equals("text/*", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(["*.txt", "*.json", "*.csv", "*.md", "*.html", "*.css", "*.js"]);
            else if (part.Equals("application/json", StringComparison.OrdinalIgnoreCase)) patterns.Add("*.json");
        }
        return patterns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string GuessMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".html" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };

    private static long SafeLong(Func<long> get)
    {
        try { return get(); } catch { return 0L; }
    }

    private sealed class BridgeRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public JsonElement Args { get; set; }
    }
}
