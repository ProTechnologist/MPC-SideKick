# MPC-SideKick

A smart, auto-hiding dual-panel companion for the MPC-BE media player, built with .NET 8 and WPF.

## Project Overview
MPC-SideKick provides two interactive side panels that "stick" to the MPC-BE window:
- **Library (MainWindow)**: Located on the left, allows browsing local folders, searching, and adding videos to the playlist.
- **Playlist (PlaylistWindow)**: Located on the right, manages the active playback queue with support for drag-and-drop reordering and shuffling.

The application uses native Windows API hooks to track the MPC-BE window's position and size, ensuring the panels stay anchored even during moves or resizing.

## Technical Stack
- **Framework**: .NET 8.0-windows (WPF)
- **Key Libraries**:
  - `Microsoft-WindowsAPICodePack-Shell`: Used for high-quality video thumbnail extraction.
  - `System.Drawing.Common`: Used for tray icon and bitmap handling.
  - `System.Text.Json`: Used for settings and playlist persistence.
- **Native Integration**: P/Invoke (User32.dll) for window positioning (`SetWindowPos`), cursor tracking (`GetCursorPos`), and event hooks (`SetWinEventHook`).

## Architecture & Key Components
- **`App.xaml.cs`**: Application entry point that bootstraps both the Library and Playlist windows.
- **`StickyWindowHelper.cs`**: The core logic for window anchoring. It monitors for the MPC-BE process, hooks into its location-change events, and synchronizes the panel positions.
- **`PlaylistManager.cs`**: A singleton service managing the shared `ObservableCollection<VideoItem>`. It handles persistence to `playlist.json`.
- **`AppSettings.cs`**: Manages configuration (e.g., media player path, last folder) persisted in `settings.json`.
- **`VideoItem.cs`**: The primary data model representing a media file, its thumbnail, and playlist status.
- **`WinApi.cs`**: Centralized P/Invoke declarations for Windows API interactions.

## Building and Running
- **Command**: `dotnet run`
- **Dependencies**: 
  - .NET 8 SDK
  - MPC-BE (default path: `C:\Program Files\MPC-BE\mpc-be64.exe`)
- **Assets**: Requires `icon.ico`, `folder-icon.png`, `pin-icon.png`, and `settings-icon.png` in the project root.

## Development Conventions
- **UI Logic**: Interactive behavior (animations, mouse tracking) is primarily handled in code-behind (`.xaml.cs`) due to heavy reliance on window handles and native events.
- **Data Binding**: Uses `ObservableCollection` and `ICollectionView` for real-time filtering and list management.
- **Concurrency**: Long-running tasks like thumbnail generation and file system enumeration are performed asynchronously using `Task.Run` and `Dispatcher.Invoke`.
- **Styling**: Modern "Deep Carbon" UI theme defined in XAML resources within the window files.

## Key Files
- `MainWindow.xaml/cs`: Left panel (Library).
- `PlaylistWindow.xaml/cs`: Right panel (Playlist).
- `SettingsWindow.xaml/cs`: Configuration interface.
- `StickyWindowHelper.cs`: Native window synchronization logic.
- `PlaylistManager.cs`: State management for the video queue.
