# MacDock

**English** · [Tiếng Việt](README.vi.md) · [Installation & usage guide](GUIDE.md)

**A Dock, a menu bar and themes inspired by macOS, for Windows 11** — written in C# / .NET 8 / WPF.
Original software: it uses no Apple logos, icons, fonts or assets. Every icon (logo, Launchpad, Trash, Wi-Fi,
battery…) is drawn from vectors.

![Menu bar](docs/screenshots/menubar.png)

![Dock](docs/screenshots/dock.png)

| Magnify on hover | Control Center | System menu |
|---|---|---|
| ![Magnify](docs/screenshots/dock-magnify.png) | ![Control Center](docs/screenshots/control-center.png) | ![System menu](docs/screenshots/menu-he-thong.png) |

| Dock settings | Themes | Calendar |
|---|---|---|
| ![Dock settings](docs/screenshots/cai-dat-dock.png) | ![Themes](docs/screenshots/cai-dat-giao-dien.png) | ![Calendar](docs/screenshots/lich.png) |

## Features

### Dock
- Cosine-curve magnification with smooth animation; adjustable size, magnification, curve width and speed.
- Bounces when an app launches or needs attention; running-app dots; name labels; shadow.
- Badges: a red dot when a window flashes, a number when the title looks like `(3) Messenger`.
- Click an icon to open the app / bring it forward / click again to minimize / cycle through its windows.
- Live window previews when hovering a running app (click to switch, × to close).
- Right-click: window list, New Window, Keep in Dock, Move Left/Right, Show in Explorer, Change Icon, Hide, Quit, Force Quit.
- Drag and drop: rearrange icons, drag out to remove, drop `.exe` / `.lnk` / folders to pin, drop files on an app to open them,
  drop files on Trash to delete them (to the Windows Recycle Bin, undoable).
- Add apps from a searchable list of everything installed (including Store apps); manage pinned apps in Settings.
- Bottom / left / right position, smart auto-hide (hides only when a window covers the Dock, shows the moment the
  pointer touches the screen edge), reserved space so maximized windows never cover it.
- Multiple displays; hides automatically for full-screen apps (games, video).

### Menu bar
- Liquid glass background (real blur of your wallpaper, adjustable), transparent, frosted, Dock-colored or solid;
  text color picked automatically from the wallpaper.
- A round logo (customizable, including your own image) opens the system menu: About This PC, Windows Settings,
  MacDock Settings, Task Manager, Sleep, Restart, Shut Down, Lock, Log Out.
- The active app's name in bold and its **real menus** (Win32 menus or UI Automation), with generic menus as a fallback.
- Right side: **system tray icons** (Unikey/EVKey, Zalo, OneDrive, PowerToys… — left/right/double click all work),
  input method, battery, Wi-Fi (network list, connect right in the popup), volume, Control Center (Wi-Fi, Bluetooth,
  brightness, volume, dark mode), notifications (Win+N) and a customizable clock with a mini calendar.

### More
- Glass volume/brightness overlay when you press the media keys (or keep the Windows one).
- Win+Space switches input language even while the Windows taskbar is hidden.
- Vietnamese / English interface.
- System-Settings-style Settings window with live preview and per-page reset; Performance page (FPS limit,
  disable animations, reduce on battery, live RAM/CPU).
- Theme engine (`theme.json`): light/dark colors, transparency, gradients, borders, corner radius, shadow, font;
  Light / Dark / Follow Windows / Scheduled; original or squircle icons; per-app icons; icon packs;
  `.macdocktheme` import/export. 6 built-in themes.
- Starts with Windows instantly via Task Scheduler (no startup delay), single instance, welcome screen.

## Installation

See the full **[installation & usage guide](GUIDE.md)**. In short:

1. Download `MacDock-win-x64.zip` from [**Releases**](https://github.com/Vudinhdat02/appThemeMacOS/releases).
2. Extract it to a permanent folder, e.g. `%LOCALAPPDATA%\Programs\MacDock`.
3. Run `MacDock.exe` (.NET is included). Choose your options on the welcome screen.

Nothing has to be changed in Windows by hand. Requirements: Windows 11 (or Windows 10 1903+), x64.

## Restoring the Windows taskbar

MacDock saves the original taskbar state before hiding it and **always restores it** when it exits, crashes, or when
you sign out / shut down. A small watchdog process restores it if MacDock is killed. At any time:

```bash
MacDock.exe --restore-taskbar
```

Or turn off **Hide the Windows taskbar** in Settings → General (keeps the original taskbar; the Dock sits above it).

## Building from source

Requires Windows 10/11 and the .NET 8 SDK (or newer).

```bash
git clone https://github.com/Vudinhdat02/appThemeMacOS.git
```

```bash
dotnet publish src/MacDock/MacDock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o dist
```

GitHub Actions (`.github/workflows/build.yml`) builds every push and publishes a Release with the zip when a `v*` tag is pushed.

## Performance (measured)

Intel Core i7-9750H, Windows 11, 1920×1080 @125%, Release single-file build.

| Metric | Result |
|---|---|
| RAM (main process, idle) | ~45–80 MB |
| RAM (watchdog) | ~10 MB |
| CPU when idle | ~0% — no frames are redrawn |
| CPU while you work in other apps | ~0.1–0.3% |

## Known limitations

- Number badges only come from window titles or flashing windows; Windows does not expose taskbar/Store badges.
- Real app menus require Win32 menus or a menu bar exposed through UI Automation; others get generic menus.
- Brightness keys are handled by hardware, so Windows may still show its own overlay.
- Tested on Windows 11 with one display; multi-monitor and ARM devices have not been tested yet.
- The executable is not code-signed yet, so SmartScreen may warn on first run; some antivirus tools may flag the keyboard hook.
- Not yet available: full-screen Launchpad, search, hot corners.

## License

[MIT](LICENSE). See also the [changelog](CHANGELOG.md).

Developer: **Vũ Đình Đạt** — [GitHub](https://github.com/Vudinhdat02) · [Website](https://vudinhdat02.github.io/)
