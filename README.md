# QuickLaunch

A glorified cookie manager for MapleStory accounts.

# Building

Requirements:
* .NET 6.0 SDK (or higher)

```
dotnet publish -r win10-x64 -c release -o publish --self-contained /p:DebugType=none /p:PublishSingleFile=true
```
Not quite "single file", but close enough.

# Runtime requirements

* [Microsoft WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)
