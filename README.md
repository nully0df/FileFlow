# FileFlow

**A place for every file.** A Windows desktop utility that organizes a folder by file type, shows every destination before moving anything, and keeps a persistent undo history.

Built with **C# · .NET 10 · WinForms**. No third-party packages. No accounts, uploads or background watcher.

![FileFlow showing a real preview of synthetic sample files](docs/fileflow-preview.png)

## What it does

- Preview files and their exact destinations before applying a batch.
- Choose individual files with checkboxes.
- Edit extension-to-folder rules; settings persist between launches.
- Resolve existing names with suffixes such as `report (1).pdf`.
- Refuse new conflicts that appear after the preview, without overwriting anything.
- Undo the latest remaining batch, including after restarting the app.
- Check file contents with SHA-256 before undoing; edited files stay where they are.
- Cancel a running operation between files or during hashing; completed moves remain undoable.
- Try a built-in sample folder containing a naming conflict and an unmatched file.

### Example

```text
Downloads/                         After preview + confirmation:
├── invoice.pdf                    Downloads/
├── photo.jpg                      ├── Documents/invoice (1).pdf
├── notes.txt                      ├── Documents/notes.txt
├── unknown.data                   ├── Images/photo.jpg
└── Documents/invoice.pdf          ├── unknown.data
                                   └── Documents/invoice.pdf  (existing file preserved)
```

The example image uses synthetic text fixtures with representative extensions, not personal documents or real media.

## Run

Requires Windows and the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). The portable build is framework-dependent; the runtime is not bundled.

With the .NET 10 SDK installed, run from the repository root:

```powershell
dotnet run --project src/FileFlow
```

1. Click **Try a sample folder**, or choose a folder you want to organize.
2. Click **Preview files**. Review the destination, status and selected checkboxes.
3. Click **Move N files** and confirm the displayed count.
4. Use **Undo latest** to restore the most recent remaining batch.

**Sorting rules** lets you edit categories, enable or disable them, add rows and remove a selected row with Delete. Extensions use a leading dot and are separated with commas. Matching ignores extension case.

![Editable rules](docs/fileflow-rules.png)

## Behavior and boundaries

FileFlow deliberately processes only files directly inside the selected folder. Existing subfolders are not scanned. Unmatched, hidden and system files are left untouched. Symbolic links, junctions and cloud placeholders are refused when detected, including in the selected folder's ancestor chain.

The preview records length and last-write time. Before moving, the app rechecks these values, checks the paths, hashes the file and writes a prepared journal entry. It uses a same-volume move without overwrite. If the destination became occupied, it skips that file and asks for a new preview.

Undo refuses an occupied original location or a destination whose SHA-256 has changed. Failed undo entries remain available for a retry; after finishing the newest batch, an earlier batch can be undone. Empty category folders are retained, and newly added files inside them are not removed.

This is an undo log, not a backup or an all-or-nothing transaction. A batch can partially succeed. The app reports skipped files separately. Prepared journal entries allow reconciliation after an interruption before or after a file move, but power-loss durability depends on the filesystem and storage. Restoring still requires the files and journal to be available. Only file contents are hashed; ACLs, timestamps and alternate data streams are not restored. Preview metadata cannot detect a same-size edit whose timestamp was deliberately preserved. Path checks do not provide protection against another process maliciously replacing paths at precisely the same time. Use ordinary local folders, not folders being actively rewritten or synced.

## Data and privacy

Settings and history are stored locally:

```text
%LOCALAPPDATA%/FileFlow/
├── settings.json
├── history/       # one JSON journal per batch, plus an operation lock
└── Samples/       # generated only when you request a sample folder
```

History contains absolute file paths and content hashes. FileFlow makes no network requests. Keep the history if you want to undo older batches; it is not automatically pruned. For an isolated portable profile:

```powershell
FileFlow.exe --data-dir "D:\FileFlowProfile"
```

## Build and verify

```powershell
dotnet restore FileFlow.sln
dotnet build FileFlow.sln -c Release --no-restore
dotnet run --project tests/FileFlow.Checks -c Release --no-build
dotnet run --project tests/FileFlow.UiChecks -c Release --no-build -- artifacts/ui
```

The package-free check runner returns a nonzero exit code on failure. It covers 30 filesystem scenarios: collisions, stale previews, partial failures, cancellation, path containment, occupied originals, modified content, persistent history and interrupted operations. The Windows UI harness exercises the actual forms with generated files, including checkbox selection, moving, undo, rule editing and settings reload. It renders ordinary and compact layouts to PNG and checks for cross-thread UI access. It supplies confirmations directly; native file-picker and message-box interaction is a separate manual check.

GitHub Actions is configured to build and run the filesystem checks on Windows. The UI harness is run locally; a successful local check is not a claim that remote CI has already run.

Create a portable build:

```powershell
dotnet publish src/FileFlow/FileFlow.csproj -c Release --self-contained false -o artifacts/FileFlow
```

Distribute the entire `artifacts/FileFlow` folder, not only the executable.

## Code map

| Area | Responsibility |
| --- | --- |
| `FileFlow.Core/Rules.cs` | Defaults and validation of editable sorting rules |
| `FileFlow.Core/Planner.cs` | Read-only scan, classification and collision planning |
| `FileFlow.Core/PathSafety.cs` | Local path boundaries and reparse-point checks |
| `FileFlow.Core/Organizer.cs` | Moving files, cancellation, hashing and undo |
| `FileFlow.Core/JournalStore.cs` | Persistent JSON history and an exclusive operation lock |
| `FileFlow/MainForm.cs` | Preview, selection and asynchronous user actions |
| `FileFlow/RulesForm.cs` | Rule editor |
| `tests/` | Filesystem and form-level checks using generated fixtures |

For a step-by-step Russian explanation and small exercises, see [Разбор проекта](docs/LEARNING.ru.md).

## Next ideas

Not implemented in v0.1: optional recursion with exclusions, date-based folder rules, an operation-history browser and rule import/export. Automatic background sorting is intentionally outside the initial scope.

## License

[MIT](LICENSE).
