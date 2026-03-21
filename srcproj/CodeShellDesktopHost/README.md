# CodeShell Desktop Host

This is a .NET 8 WPF + WebView2 desktop wrapper for the uploaded CodeShell HTML project.

What changed:
- Boots in **Environment + Files** mode by default.
- Adds a Settings panel so you can switch to **Data Mode** at runtime.
- Keeps SQLite-backed key/value storage and blob storage.
- Adds native file dialogs for upload/download.
- Adds a native filesystem bridge so core shell commands operate on real Windows folders and files when Environment + Files mode is active.

Supported native-mode commands:
- `pwd`, `cd`, `ls`, `dir`, `tree`
- `mkdir`, `touch`, `cat`, `open`, `edit`
- `rm`, `del`, `cp`, `mv`, `stat`
- `head`, `tail`, `find`, `grep`
- `ul`, `dl`, `df`, `storage`
- `mode`, `hostinfo`

Notes:
- In **Data Mode**, the original browser-side virtual filesystem and CodeShell app/data behavior remain available.
- In **Environment + Files** mode, unsupported legacy commands will tell you to switch to Data Mode.
- The project now targets .NET 8 so it can be opened in current Visual Studio releases and built more reliably in standard Windows desktop environments.

## Build

```bat
dotnet restore
dotnet build -c Debug
publish-win-x64.bat
```
