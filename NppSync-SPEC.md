# NppSync — Notepad++ Cross-Machine Tab Sync Plugin

## Product Spec v1.0

---

## Overview

NppSync is a Notepad++ plugin (.NET/C#) that syncs unsaved scratch tabs across multiple machines via a shared folder (Google Drive). When a user creates a new tab on any machine, it's assigned a fun two-word name (e.g., "fuzzy-lobster"), auto-saved to a shared folder, and automatically appears as a tab on all other machines running Notepad++ with the plugin.

## Goals

- Preserve the zero-friction "Ctrl+N and start typing" workflow
- Sync unsaved scratch notes across machines in near-real-time
- Avoid filename collisions across machines
- Fun, memorable tab names instead of "new 1", "new 2"
- Last-write-wins conflict resolution
- Minimal configuration — just set the shared folder path

---

## Technical Foundation

### Plugin Framework

- **Template**: [NppCSharpPluginPack](https://github.com/molsonkiko/NppCSharpPluginPack) (actively maintained C# plugin template)
- **Target**: .NET Framework 4.8 (required by Notepad++ plugin architecture)
- **Architecture**: Build for both x86 and x64, ship both DLLs
- **Plugin DLL Name**: `NppSync.dll`
- **Plugin folder**: `<Npp Install>/plugins/NppSync/NppSync.dll`

### Key Dependencies

- NppCSharpPluginPack infrastructure (Scintilla gateway, Npp gateway, Win32 interop)
- `System.IO.FileSystemWatcher` + polling fallback for folder monitoring
- No external NuGet packages (everything via .NET Framework BCL)

---

## Architecture

### Components

```
┌─────────────────────────────────────────────┐
│                  NppSync Plugin              │
├─────────────────────────────────────────────┤
│  TabManager          - Tracks managed tabs  │
│  NameGenerator       - Fun two-word names   │
│  FileSyncService     - Read/write to shared │
│  FolderWatcher       - Detect remote changes│
│  AutoSaver           - Debounced local saves│
│  SettingsManager     - Config persistence   │
│  ConflictResolver    - Last-write-wins      │
└─────────────────────────────────────────────┘
         │                        │
         ▼                        ▼
  ┌──────────────┐    ┌─────────────────────┐
  │  Notepad++   │    │  Shared Folder      │
  │  (Scintilla) │    │  (Google Drive)     │
  └──────────────┘    │                     │
                      │  fuzzy-lobster.txt  │
                      │  turbo-pancake.txt  │
                      │  .nppsync/          │
                      │    meta.json        │
                      │    machine-id.json  │
                      └─────────────────────┘
```

### Shared Folder Structure

```
<SyncFolder>/                         # e.g., G:\My Drive\NppSync\
├── fuzzy-lobster.txt                 # Note content (plain text)
├── turbo-pancake.txt
├── quantum-donut.txt
└── .nppsync/                         # Metadata folder (created with Hidden file attribute)
    ├── fuzzy-lobster.meta.json       # Per-note metadata
    ├── turbo-pancake.meta.json
    └── config.json                   # Shared config (future use)
```

The `.nppsync/` directory must be created with `FileAttributes.Hidden` so it doesn't clutter the sync folder in Explorer or the Google Drive web UI.

### Metadata File Format (per note)

```json
{
  "name": "fuzzy-lobster",
  "createdBy": "WORK-PC",
  "createdAt": "2026-04-08T14:30:00Z",
  "lastModifiedBy": "HOME-PC",
  "lastModifiedAt": "2026-04-08T16:45:00Z",
  "deleted": false
}
```

---

## Core Behaviors

### 1. New Tab Creation

**Trigger**: User creates a new unsaved tab (Ctrl+N) or the plugin detects a new "new N" tab.

**Flow**:
1. Plugin intercepts `NPPN_FILEOPENED` notification (fires when a new document is created; do **not** use `NPPN_BUFFERACTIVATED` here — that fires on every tab switch and would cause duplicate processing)
2. Detects it's an unsaved "new" tab by calling `NPPM_GETFULLCURRENTPATH` — returns an empty string for unsaved documents
3. Generates a unique two-word name (e.g., "cosmic-waffle")
4. Checks shared folder — if name collision, regenerate
5. Calls `NPPM_SAVECURRENTFILEAS` with the full target path `<SyncFolder>/cosmic-waffle.txt` — this both writes the file to disk and rebinds the buffer's associated path in Notepad++. Must be called synchronously on the UI thread before any further operations on this buffer.
6. Creates `<SyncFolder>/.nppsync/cosmic-waffle.meta.json`
7. Registers the tab in the local `TabManager`

**Important**: The tab becomes a *saved file* at `<SyncFolder>/cosmic-waffle.txt`. This means:
- The tab title shows `cosmic-waffle` or `cosmic-waffle.txt` depending on the user's Notepad++ "Hide extension in title bar" setting — the plugin cannot control this
- Notepad++ treats it as a regular saved file
- Auto-save is handled by the plugin writing to this file path
- Because the plugin calls `SCI_SETSAVEPOINT` after every auto-save, the buffer is always "clean" when close is triggered — Notepad++ will not show a save dialog (no interception needed)

### 2. Auto-Save (Local Edits → Shared Folder)

**Trigger**: User types in a managed tab.

**Flow**:
1. Plugin listens for `SCN_MODIFIED` notifications on managed tabs. **`SCN_MODIFIED` fires on every single keystroke, paste, undo, and redo — the handler must be zero-cost: reset a timer and return immediately. No file I/O, no string allocation, no heap pressure.**
2. Starts a debounce timer (default: 3 seconds of inactivity)
3. On timer fire: reads full buffer content via Scintilla API
4. Writes content to `<SyncFolder>/<name>.txt`
5. Updates metadata `lastModifiedBy` and `lastModifiedAt`
6. Marks the tab as "clean" (no unsaved indicator dot) via `SCI_SETSAVEPOINT` (takes no arguments — marks the current buffer state as the save point)

**Debounce strategy**: 
- Reset timer on every keystroke
- Force-save after 10 seconds even if still typing (max delay)
- Immediate save on tab switch (`NPPN_BUFFERACTIVATED` away from managed tab)
- Immediate save on Notepad++ losing focus (`NPPN_SHUTDOWN`, `WM_KILLFOCUS`)

### 3. Remote File Detection (Shared Folder → Local Tabs)

**Trigger**: A new file appears in the shared folder that wasn't created by this machine (matching the configured extension).

**Thread safety**: `FileSystemWatcher` callbacks fire on a background thread pool. All Notepad++ and Scintilla API calls (`NPPM_DOOPEN`, `SCI_SETTEXT`, etc.) must happen on the main UI thread. Marshal every watcher callback to the UI thread by posting a Windows message (e.g., `SendMessage`/`PostMessage` to the Notepad++ HWND) or using `NativeWindow` + `WndProc` interception to dispatch the work. Never call Npp/Scintilla APIs directly from a watcher callback.

**Flow**:
1. `FolderWatcher` uses `FileSystemWatcher` on the shared folder
2. Polling fallback every 5 seconds checks for new/modified files
3. On detecting a new file (after marshaling to UI thread):
   a. Read the `.meta.json` — if `createdBy` matches this machine, skip (echo)
   b. If no meta file yet (race condition), retry up to 3 times with 500ms delay between attempts; skip if meta never appears
   c. **Duplicate check**: Call `NPPM_GETOPENFILENAMES` to get all currently open file paths. If this file is already open, skip (avoids double-opening on startup or race conditions)
   d. Open the file in Notepad++ via `NPPM_DOOPEN`
   e. Register it in the local `TabManager`
4. On detecting a modified file for an already-open tab (after marshaling to UI thread):
   a. Read meta — if `lastModifiedBy` matches this machine, skip (echo)
   b. Read new content from disk
   c. Update the Scintilla buffer via `SCI_SETTEXT` (preserving cursor if possible). **Note**: `SCI_SETTEXT` replaces the entire buffer and clears the undo history — this is a known v1 limitation. The user will lose Ctrl+Z for prior edits when a remote update is received.
   d. Mark as clean via `SCI_SETSAVEPOINT`

**Anti-echo logic**: Every write the plugin does locally is tracked with a timestamp. If a file change is detected within 2 seconds of a local write to the same file, it's assumed to be an echo from Google Drive re-syncing and is ignored.

### 4. Tab Close

**Trigger**: User closes a managed synced tab (click X, Ctrl+W, etc.).

**Flow**:
1. Plugin intercepts `NPPN_FILEBEFORECLOSE`
2. If it's a managed NppSync tab:
   a. Auto-save current content to shared folder and call `SCI_SETSAVEPOINT` to mark the buffer clean
   b. Because the buffer is always kept clean via `SCI_SETSAVEPOINT` after every auto-save, Notepad++ will not show a "Save changes?" dialog — no suppression mechanism is needed
   c. Show a custom NppSync dialog:

      ```
      ┌─────────────────────────────────────────┐
      │  Close synced note "cosmic-waffle"?      │
      │                                          │
      │  [Dismiss]  [Delete Everywhere]  [Cancel]│
      └─────────────────────────────────────────┘
      ```

   d. **Dismiss**: 
      - Close the tab locally
      - Remove from local `TabManager`
      - Add the note name to a local ignore list (`<Npp Plugin Config>/NppSync.dismissed.json`)
      - File remains in sync folder — other machines are unaffected
      - Note will NOT reopen on next startup (ignored during scan)
      - Can be recovered via `Plugins > NppSync > Show Dismissed Notes` (for selective recovery) or `Plugins > NppSync > Restore All Dismissed Notes` (which clears the entire dismissed list and re-scans — **warning: reopens all previously dismissed notes**)
   
   e. **Delete Everywhere**:
      - Close the tab locally
      - Remove from local `TabManager`
      - Set `deleted: true` in the note's `.meta.json`
      - Delete the `.txt` file from the sync folder
      - On other machines: watcher detects deletion and closes the tab silently (no prompt)
   
   f. **Cancel**:
      - Abort the close, tab stays open

**Dismissed notes file** (`NppSync.dismissed.json`, local only, not synced):
```json
{
  "dismissed": [
    { "name": "cosmic-waffle", "dismissedAt": "2026-04-08T14:30:00Z" },
    { "name": "old-pretzel", "dismissedAt": "2026-04-01T09:00:00Z" }
  ]
}
```

**Remote deletion handling**: When another machine deletes a note (watcher detects `.txt` file removed or `deleted: true` in metadata), close the tab silently on this machine — no prompt needed, since the delete was intentional from another machine.

### 5. Note Graduation (Save As)

**Trigger**: User performs Save As (`Ctrl+Shift+S`) on a managed synced tab, saving it to a location outside the sync folder.

**Flow**:
1. Plugin listens for `NPPN_FILESAVED`. Note: `NPPN_FILERENAMED` does not exist as a distinct notification in Notepad++. When the user does Save As, Notepad++ fires `NPPN_FILESAVED` at the new path. The plugin detects a "rename" by comparing the new path (from `NPPM_GETFULLCURRENTPATH` at notification time) against the path recorded in `TabManager` for that buffer ID — if they differ, it's a Save As.
2. After the save, checks if the buffer's file path still points to the sync folder
3. If the path has changed to a location **outside** the sync folder:
   a. Remove the tab from `TabManager` — it's no longer a synced note
   b. Delete the original `<name>.txt` from the sync folder
   c. Delete the corresponding `<name>.meta.json` from `.nppsync/`
   d. Stop watching/auto-saving this buffer
   e. The tab is now a normal Notepad++ file at the new location
4. On other machines: the watcher detects the `.txt` deletion and closes the tab

**Lifecycle**: Every note follows one of these paths:
```
                                         ┌→  Save As outside sync  →  regular file (graduated)
                                         │
Ctrl+N / Ctrl+Shift+N  →  synced note  ──┼→  Close → Dismiss       →  hidden locally (recoverable)
    (cosmic-waffle)       (auto-synced)  │
                                         └→  Close → Delete         →  gone everywhere
```

**Edge case**: If the user does Save As to a *different path inside the sync folder*, the plugin should treat this as a rename — update `TabManager` with the new filename, delete the old `.txt` and `.meta.json`, create new metadata for the renamed file. This lets users manually rename notes to something meaningful while keeping them synced.

### 6. Startup Behavior

**Flow** on Notepad++ launch:
1. Plugin initializes, reads settings (shared folder path, machine ID)
2. If no machine ID exists, generate one: `<COMPUTERNAME>-<4 random hex chars>`
3. Load the local dismissed notes list (`NppSync.dismissed.json`)
4. Scans shared folder for all `.txt` files
5. For each file where `deleted` is not true AND name is not in the dismissed list:
   a. Open in Notepad++ via `NPPM_DOOPEN`
   b. Register in `TabManager`
6. Start `FolderWatcher` for ongoing sync
7. **Startup delay**: Wait 5 seconds before scanning to let Google Drive sync settle

### 7. Conflict Resolution (Last Write Wins)

Since Google Drive handles file-level sync:
- If two machines edit the same file simultaneously, Google Drive will sync the last-written version
- The plugin on the "losing" machine will detect the file change and update its buffer
- Cursor position may jump — acceptable for v1
- No merge, no conflict files, no user prompts

---

## Name Generator

### Design

Two curated word lists — adjectives and nouns — embedded as static string arrays in the plugin.

**Format**: `<adjective>-<noun>` (lowercase, hyphen-separated)

### Word Lists (200 each — representative sample, full lists in implementation)

**Adjectives** (fun, vivid, non-offensive):
```
fuzzy, turbo, cosmic, sneaky, dizzy, velvet, neon, quantum, sleepy, grumpy,
wobbly, zesty, crispy, phantom, solar, atomic, rusty, bouncy, mystic, pixel,
copper, frozen, golden, hollow, ivory, jolly, lunar, marble, nimble, orange,
plucky, quirky, rapid, silver, tangy, ultra, vivid, wicked, zippy, amber,
blazing, chunky, dapper, eager, frosty, gentle, hasty, icy, jazzy, keen,
lanky, mellow, nutty, olive, peppy, quaint, rosy, spicy, toasty, urban,
witty, young, zealous, bold, calm, dark, easy, fair, glad, hazy,
idle, jumpy, kind, lazy, mild, neat, odd, pale, quiet, rich,
safe, tall, vast, warm, extra, fresh, grand, happy, iron, lucky,
magic, noble, opal, proud, royal, sharp, swift, tough, wise, brave,
coral, dusty, elfin, fiery, giddy, hefty, itchy, jiffy, knack, lofty,
merry, nerdy, onyx, perky, rebel, smoky, timid, vivid, wacky, yappy,
...
```

**Nouns** (concrete, visual, memorable):
```
lobster, pancake, waffle, thunder, mango, donut, falcon, cactus, penguin, pretzel,
walrus, badger, turnip, rocket, pebble, dragon, otter, pickle, comet, acorn,
biscuit, candle, dagger, eclipse, ferret, goblin, hammer, igloo, jigsaw, kettle,
lantern, muffin, nebula, orchid, parrot, quasar, raven, sphinx, teapot, urchin,
violin, whistle, falcon, yeti, zephyr, anchor, button, cobalt, delta, ember,
flagon, gazelle, hermit, indigo, jackal, kitten, lemon, marmot, napkin, ocelot,
pirate, quiver, riddle, saddle, tangle, unison, vortex, wombat, bobcat, summit,
tablet, gadget, helmet, insect, jersey, kernel, lizard, magnet, nickel, osprey,
piston, raptor, shovel, tinker, vessel, wasp, carbon, drifter, falcon, gibbon,
hobbit, impala, jumble, kennel, llama, moose, newt, oyster, puffin, rascal,
socket, toucan, vulcan, walnut, yonder, zipper, alpaca, beetle, condor, donkey,
falcon, gopher, hornet, iguana, jaguar, koala, lemur, mantis, narwhal, okapi,
...
```

**Collision handling**: If `<adjective>-<noun><ext>` already exists in the shared folder, regenerate. With 200×200 = 40,000 combinations, collisions are extremely rare. `<ext>` is the configured file extension (default `.txt`).

**Uniqueness**: Use `Random` seeded with `Environment.TickCount` + machine hash for variety across machines.

---

## Settings

### Storage

Settings stored in `<Npp Plugin Config>/NppSync.ini`:

```ini
[NppSync]
SyncFolder=G:\My Drive\NppSync
MachineId=WORK-PC-a3f7
FileExtension=.txt
AutoSaveDelaySec=3
MaxAutoSaveDelaySec=10
PollIntervalSec=5
StartupDelaySec=5
OpenRemoteTabsAutomatically=true
```

### Settings UI

A simple WinForms dialog accessible via `Plugins > NppSync > Settings`:
- **Sync Folder**: Text box + Browse button (folder picker)
- **Machine ID**: Display-only (auto-generated, editable for advanced users)
- **File Extension**: Dropdown (`.txt`, `.md`, `.js`, `.py`, `.cs`, or custom text entry). Determines the extension used for new synced notes. Changing this only affects new notes — existing notes keep their original extension. Using `.md` gives you free Markdown syntax highlighting in Notepad++.
- **Auto-save delay**: Numeric spinner (1-30 seconds)
- **Poll interval**: Numeric spinner (1-60 seconds)
- **Auto-open remote tabs**: Checkbox

---

## Plugin Menu

Under `Plugins > NppSync`:

| Menu Item | Action |
|-----------|--------|
| **New Synced Note** | Creates a new tab with a generated name (alternative to Ctrl+N interception) |
| **Delete This Note Everywhere** | Deletes the current note from shared folder + all machines (skips the close prompt) |
| **Rename This Note** | Generates a new fun name (re-saves with new name, deletes old) |
| **Open Sync Folder** | Opens the shared folder in Explorer |
| **Restore All Dismissed Notes** | Clears the entire dismissed list and re-scans — reopens **all** previously dismissed notes |
| **Show Dismissed Notes** | Opens a dialog listing dismissed notes with options to restore or permanently delete each one |
| --- | --- |
| **Settings** | Opens settings dialog |
| **About** | Version info |

### Keyboard Shortcuts (configurable via Notepad++ shortcut mapper)

| Default Shortcut | Action |
|-----------------|--------|
| `Ctrl+Shift+N` | New Synced Note |
| `Ctrl+Shift+D` | Delete This Synced Note |

---

## Notepad++ API Integration

### Notifications to Handle

| Notification | Purpose |
|-------------|---------|
| `NPPN_READY` | Plugin initialization after Npp fully loaded |
| `NPPN_SHUTDOWN` | Save all managed tabs, stop watcher |
| `NPPN_FILEOPENED` | Detect new tabs (fires when a new document is created via Ctrl+N or File > New) |
| `NPPN_FILESAVED` | Detect Save As — compare new path against `TabManager` record to identify renames/graduations |
| `NPPN_FILEBEFORECLOSE` | Auto-save + mark buffer clean for managed tabs (no dialog suppression needed) |
| `NPPN_BUFFERACTIVATED` | Save previous buffer if managed, track active tab |
| `SCN_MODIFIED` | Content change → reset debounce timer only (handler must be zero-cost) |
| `SCN_SAVEPOINTREACHED` | Tab marked as saved |
| `SCN_SAVEPOINTLEFT` | Tab marked as modified |

### Scintilla Messages Used

| Message | Purpose |
|---------|---------|
| `SCI_GETTEXT` / `SCI_GETLENGTH` | Read buffer content for saving |
| `SCI_SETTEXT` | Update buffer with remote content |
| `SCI_SETSAVEPOINT` | Clear the "modified" indicator after auto-save |
| `SCI_GETCURRENTPOS` / `SCI_SETCURRENTPOS` | Preserve cursor on remote update |
| `SCI_GETFIRSTVISIBLELINE` / `SCI_SETFIRSTVISIBLELINE` | Preserve scroll on remote update |

### Notepad++ Messages Used

| Message | Purpose |
|---------|---------|
| `NPPM_DOOPEN` | Open a file as a new tab |
| `NPPM_GETFULLCURRENTPATH` | Get file path of active tab |
| `NPPM_GETOPENFILENAMES` | Get all currently open file paths (for duplicate detection before opening) |
| `NPPM_GETBUFFERIDFROMPOS` | Map tab positions to buffer IDs |
| `NPPM_MENUCOMMAND` | Trigger built-in commands |
| `NPPM_SAVECURRENTFILEAS` | Save current buffer to a specific path |

---

## Edge Cases & Robustness

### Google Drive Not Running / Offline
- Plugin works normally — files save to the local Google Drive folder
- When Drive reconnects, it syncs automatically
- No special handling needed in the plugin

### Shared Folder Doesn't Exist on Startup
- Show a notification bar or message box: "NppSync sync folder not found. Please configure in Settings."
- Plugin enters dormant mode — no file watching, no auto-save
- Re-check when settings are saved

### File Locked by Google Drive
- Google Drive occasionally locks files briefly during sync
- Wrap all file writes in retry logic: 3 attempts, 500ms delay between each
- Log failures but don't show UI errors for transient locks

### Very Large Files
- If a synced note exceeds 1MB, skip auto-save and warn the user
- These are scratch notes — they shouldn't be huge

### Save As / Note Graduation
- Handled by Section 5 (Note Graduation). Save As outside sync folder = clean removal from sync.
- Save As *inside* sync folder = treated as a rename (old file deleted, new one tracked).

### Write Atomicity (`.txt` + `.meta.json`)

Creating or updating a note requires two separate file writes. A crash or power loss between them leaves inconsistent state (e.g., a `.txt` with no corresponding `.meta.json`). The startup scan must tolerate this: if a `.txt` exists with no `.meta.json`, either skip it (safest for v1) or synthesize minimal metadata and open it anyway. This is a known v1 limitation — no atomic two-file commit mechanism is implemented.

### Machine ID Lost (Settings Reset)

If `NppSync.ini` is deleted or the machine ID entry is removed, the plugin generates a new machine ID on next launch. This causes the machine to re-open all notes it had previously dismissed (dismissed notes are keyed by note name, not machine ID, so this is fine) but it will also appear as a "new machine" in metadata — not harmful, just worth noting.

### Multiple Notepad++ Instances on Same Machine
- Only one instance should manage sync (the first one to start)
- Use a named mutex (`NppSync_<SyncFolderHash>`) to coordinate
- Second instance shows tabs read-only or skips sync

---

## Implementation Phases

### Phase 1: Foundation (MVP)
- [ ] Set up NppCSharpPluginPack project structure
- [ ] Settings manager (INI read/write, settings dialog)
- [ ] Name generator with embedded word lists
- [ ] Manual "New Synced Note" menu command
- [ ] Auto-save on debounced timer
- [ ] Basic folder watcher + polling fallback
- [ ] Open remote files as tabs on detection
- [ ] Startup scan of sync folder
- [ ] Note graduation (Save As outside sync folder cleans up synced note)

### Phase 2: Polish
- [ ] Intercept Ctrl+N to auto-create synced notes
- [ ] Anti-echo logic for ignoring self-writes
- [ ] Cursor/scroll preservation on remote updates
- [ ] Delete note command
- [ ] Rename note command
- [ ] Named mutex for multi-instance safety

### Phase 3: Nice-to-Haves
- [ ] System tray toast notifications for remote notes
- [ ] "Archive" old notes (move to subfolder after N days)
- [ ] Note search/filter dialog
- [ ] Markdown preview for synced notes
- [ ] Encryption option for sensitive notes

---

## Build & Deployment

### Build

NppCSharpPluginPack uses a classic `.csproj` (not SDK-style), so use `msbuild` rather than `dotnet build`:

```bash
# From solution root
msbuild NppSync.sln /p:Configuration=Release /p:Platform=x64
msbuild NppSync.sln /p:Configuration=Release /p:Platform=x86
```

Output:
- `bin/Release/x64/NppSync.dll`
- `bin/Release/x86/NppSync.dll`

### Install

Copy to Notepad++ plugins folder:
```
C:\Program Files\Notepad++\plugins\NppSync\NppSync.dll       # x64
C:\Program Files (x86)\Notepad++\plugins\NppSync\NppSync.dll # x86
```

### First Run

1. Launch Notepad++
2. `Plugins > NppSync > Settings`
3. Set sync folder to Google Drive path (e.g., `G:\My Drive\NppSync`)
4. Click OK
5. Plugin creates `.nppsync/` metadata subfolder
6. Start creating notes with `Ctrl+Shift+N` or `Plugins > NppSync > New Synced Note`
