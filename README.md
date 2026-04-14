# DraftSync

A Notepad++ plugin that syncs scratch notes across machines via a shared folder (Google Drive, OneDrive, Dropbox, or any synced folder).

If you like to fire up Notepad++ to take a quick note, and go to another machine and wish your scratch notes follow, this plugin is for you.

Create a note on your desktop, and it appears instantly on your laptop. No accounts, no servers, just a folder.

---

## How it works

When you create a new tab, DraftSync saves it to your shared folder with a generated two-word name (e.g. `fuzzy-lobster.txt`). The plugin watches the folder for changes from other machines and silently keeps all open notes in sync.

Notes are plain text files. You can open, read, and edit them from any machine, even without Notepad++.

---

## Features

- **Auto-sync** — edits propagate to all machines in near-real-time
- **Auto-save** — notes are saved automatically as you type (debounced, 3s default)
- **Fun names** — notes get memorable two-word names instead of "new 1", "new 2"
- **Silent refresh** — updated notes reload in the background without prompts
- **Close options** — dismiss a note locally, or delete it from all machines at once
- **Startup scan** — all notes reopen automatically when Notepad++ starts
- **No conflict files** — last write wins; Google Drive handles the sync

---

## Requirements

- Notepad++ 8.x or later (x64 or x86)
- .NET Framework 4.8 (included in Windows 10/11)
- A shared folder synced across your machines (Google Drive, OneDrive, Dropbox, etc.)

---

## Installation

1. Download the latest release from the [Releases](https://github.com/belarrem/DraftSync/releases) page
2. Copy the correct DLL for your Notepad++ version into its plugins folder:

   ```
   # 64-bit Notepad++
   C:\Program Files\Notepad++\plugins\DraftSync\DraftSync.dll

   # 32-bit Notepad++
   C:\Program Files (x86)\Notepad++\plugins\DraftSync\DraftSync.dll
   ```

3. Restart Notepad++
4. Go to **Plugins > DraftSync > Settings** and set the sync folder path (e.g. `G:\My Drive\DraftSync`)

---

## Usage

| Action | How |
|--------|-----|
| Create a new synced note | `Ctrl+N` (works automatically once the sync folder is configured) |
| Delete a note everywhere | `Ctrl+Shift+D` or **Plugins > DraftSync > Delete This Note Everywhere** |
| Rename a note | **Plugins > DraftSync > Rename This Note** |
| Open the sync folder | **Plugins > DraftSync > Open Sync Folder** |
| Restore dismissed notes | **Plugins > DraftSync > Restore All Dismissed Notes** |

When closing a synced note, you'll be asked:
- **Dismiss** — close locally, leave the file for other machines
- **Delete Everywhere** — remove from the sync folder and close on all machines
- **Cancel** — keep the tab open

### Saving a note as a regular file

If you want to "graduate" a synced note into a regular file, just use **Save As** (`Ctrl+Shift+S`) and pick a location outside your sync folder. The plugin will:

- Remove the note from the sync folder
- Stop syncing it on all machines (other machines will silently close their copy)
- Leave your tab open as a normal Notepad++ file at the new location

If you Save As to a different path **inside** the sync folder, it's treated as a rename — the old note is replaced and sync continues under the new name.

---

## Building from source

DraftSync targets .NET Framework 4.8 and requires Visual Studio 2022 (or Build Tools). Use `MSBuild.exe` directly — `dotnet build` is not supported.

```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

# x64
& $msbuild DraftSync.csproj /t:Restore /p:Platform=x64
& $msbuild DraftSync.csproj /p:Configuration=Release /p:Platform=x64

# x86
& $msbuild DraftSync.csproj /t:Restore /p:Platform=x86
& $msbuild DraftSync.csproj /p:Configuration=Release /p:Platform=x86
```

Output: `bin\Release-x64\DraftSync.dll` (64-bit) and `bin\Release\DraftSync.dll` (32-bit)

The project is based on [NppCSharpPluginPack](https://github.com/molsonkiko/NppCSharpPluginPack).

---

## Settings

You can access the settings from the Notepad++ menu > Plugins > DraftSync > Settings
Settings are stored in `%APPDATA%\Notepad++\plugins\Config\DraftSync\DraftSync.ini`.
The only setting you need to change is SyncFolder, the rest you can leave as default

| Setting                       | Default      | Description                                          |
|-------------------------------|--------------|------------------------------------------------------|
| `SyncFolder`                  | *(required)* | Path to your shared sync folder                      |
| `FileExtension`               | `.txt`       | Extension for new notes (`.txt`, `.md`, etc.)        |
| `AutoSaveDelaySec`            | `3`          | Seconds of inactivity before auto-save               |
| `MaxAutoSaveDelaySec`         | `10`         | Force-save after this many seconds even while typing |
| `PollIntervalSec`             | `5`          | Polling interval fallback for folder changes         |
| `StartupDelaySec`             | `5`          | Delay before startup scan (lets Drive settle)        |
| `OpenRemoteTabsAutomatically` | `true`       | Auto-open notes created on other machines            |

---

## License

[MIT](LICENSE)
