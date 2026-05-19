# Plan: Sleepy Cat UI Redesign

## Overview
Completely rebuild the Obsidian Launcher UI from scratch using the "Sleepy Cat" design system (Catppuccin-inspired, soft/pastel/rounded). Remove the FluentAvalonia dependency. All backend code (Services, Models, Utils, Settings, Enums) stays untouched.

---

## Design System Summary (from handoff)

**Color palette:** Catppuccin Latte (light) / Catppuccin Mocha (dark).  
**Primary:** mauve (#8839ef light / #cba6f7 dark), secondary: pink (#ea76cb), accent: peach (#fe640b).  
**Fonts:** Quicksand (body/UI — workhorse), Cause (display only), Playwrite GB S (accent/handwritten sparingly).  
**Radii:** everything soft (6–40px pill), never sharp corners.  
**Shadows:** soft, tinted toward primary.

**Shell layout:**
```
Window (custom chrome, no OS title bar)
├── TitleBar (44px) — traffic-light dots, app name, connection status
└── Body
    ├── Sidebar (240px) — brand, account chip, nav (Library + System sections), sleeping-cat card
    └── Content
        ├── ContentHeader (64px) — page title, search/filters, CTAs
        ├── ContentArea (*)  — routed screen
        └── StatusBar (30px) — Java version, instance count, disk, ping
```

**Screens (sidebar routed):**
- Instances — grid/list/split views, featured hero card, search, filter tabs
- Mod Browser — Modrinth search with category sidebar
- Worlds — world cards grid
- Screenshots — screenshot gallery
- Console — live log viewer (color-coded lines, auto-scroll, pause/resume)
- Accounts — account list + Microsoft sign-in
- Settings — General / Appearance (dark+accent) / Downloads / Java / About

**Modals (overlay in main window, not separate windows):**
- Create Instance — 3-step wizard (name+palette → version → mod loader)
- Instance Settings — tabbed (General / Java & Memory / Game Window / Mod Loader / Commands)
- Crash Report — already exists, keep + restyle

---

## File Changes

### Remove / Delete
| File | Reason |
|------|--------|
| `Views/SettingsDialog.axaml` + `.cs` | Uses FluentAvalonia `ContentDialog`; settings move into sidebar `SettingsView` |
| Existing `Views/SettingsWindow.axaml` + `.cs` | Replaced by embedded `SettingsView` |

### Modify
| File | What changes |
|------|-------------|
| `Obsidian Launcher.csproj` | Remove `FluentAvaloniaUI`; add `<AvaloniaResource Include="Fonts\**" />` |
| `App.axaml` | Remove FluentAvalonia xmlns + `FluentAvaloniaTheme`; include `SleepyCatTheme.axaml` |
| `App.axaml.cs` | Remove FluentAvalonia init; wire `RequestedThemeVariant` |
| `Views/MainWindow.axaml` | Complete rewrite — custom chrome shell (TitleBar, Sidebar, Content, StatusBar) |
| `Views/MainWindow.axaml.cs` | Remove `ContentDialog`; replace delete-confirm with simple Avalonia `Window`; wire drag |
| `ViewModels/MainWindowViewModel.cs` | Add routing props (`CurrentRoute`, `IsXxxScreen`), nav commands, `IsDarkMode`, modal flags (`IsCreateInstanceOpen`, `IsInstanceSettingsOpen`); remove old toolbar/newsbar props; redirect Settings/Accounts commands to sidebar navigation |

### Create (new files)
| File | Purpose |
|------|---------|
| `Fonts/Quicksand-VariableFont_wght.ttf` | Body font (copied from handoff) |
| `Fonts/Cause-VariableFont_wght.ttf` | Display font (copied from handoff) |
| `Fonts/PlaywriteGBS-VariableFont_wght.ttf` | Accent font (copied from handoff) |
| `Styles/SleepyCatTheme.axaml` | Full design system — colors (light+dark ThemeDictionaries), brushes, font resources, control styles (Button variants, TextBox, cards, pills, nav items, console frame, etc.) |
| `Controls/BlockArtControl.axaml` + `.cs` | 16×9 Minecraft pixel-art panel; `Palette` property; renders deterministic colored UniformGrid |
| `Controls/PixelCatControl.axaml` | Pixel cat mascot using inline Canvas rectangles |
| `Views/InstancesView.axaml` + `.cs` | Main instances screen (grid/list views, hero card, search/filter, empty state) |
| `Views/ConsoleView.axaml` + `.cs` | Embedded console log screen (reads from `InMemoryLogSink.Instance`) |
| `Views/SettingsView.axaml` + `.cs` | Global settings screen (General/Appearance/Downloads/Java/About tabs) |
| `Views/AccountsView.axaml` + `.cs` | Account management screen |
| `Views/ModBrowserView.axaml` + `.cs` | Mod browser screen (wire to existing `ModrinthClient`) |
| `Views/WorldsView.axaml` + `.cs` | Worlds screen (stub — uses world data from instances) |
| `Views/ScreenshotsView.axaml` + `.cs` | Screenshots gallery screen |
| `Views/CreateInstanceOverlay.axaml` + `.cs` | 3-step create-instance wizard overlay (shown in-window when `IsCreateInstanceOpen`) |
| `Views/InstanceSettingsOverlay.axaml` + `.cs` | Instance settings overlay (shown in-window when `IsInstanceSettingsOpen`) |

---

## Key Technical Decisions

1. **No FluentAvalonia** — Use `Avalonia.Themes.Fluent` as the base (for reliable control templates), then override all visual styling via `SleepyCatTheme.axaml`. Style classes map to CSS classes in the design.

2. **Routing** — `MainWindowViewModel.CurrentRoute` (string) drives which `UserControl` is visible in the content area via `IsXxxScreen` boolean properties bound to `IsVisible`. No IoC/router library needed.

3. **Overlays** — Create/edit instance modals are `UserControl`s overlaid inside the main window using an overlay `Panel` with a semi-transparent scrim. Avoids the old `ShowDialog<T>` pattern.

4. **BlockArt** — `ItemsControl` with `UniformGrid` (16 cols, 9 rows) of `Rectangle`s. Colors computed in code-behind from palette definitions + hardcoded pattern string.

5. **Dark/Light theme** — Toggled via `Application.Current.RequestedThemeVariant`. `SleepyCatTheme.axaml` provides `ThemeDictionaries` with Light/Dark variants.

6. **Console view** — `ConsoleView` reads from `InMemoryLogSink.Instance` (singleton already in codebase). When a game launches, `CurrentRoute` switches to `"console"` automatically. The existing `ConsoleWindow` popup is removed; the embedded view replaces it.

7. **Fonts** — Referenced as `avares://Obsidian Launcher/Fonts#Quicksand` etc. Applied globally on the Window root.

8. **Delete-confirm dialog** — Replace `FluentAvalonia.ContentDialog` with a standard Avalonia `Window` subclass (`ConfirmDialog`) used as a modal.

---

## End-to-End Test Checklist
After implementation:
- [ ] `dotnet build "Obsidian Launcher.sln"` — zero errors
- [ ] App launches with custom title bar, sidebar, instances grid
- [ ] Dark/Light theme switch works (Appearance settings tab)
- [ ] New instance wizard (3 steps) creates an instance
- [ ] Instance settings overlay opens, saves name/memory changes
- [ ] Launching an instance navigates to Console tab, shows output
- [ ] Delete instance shows confirm overlay, backup created
- [ ] Settings screen renders all tabs (General/Appearance/Downloads/Java/About)
- [ ] Accounts screen renders
- [ ] Mod Browser screen renders with search
