# AGENTS.md

## Project Intent

This project is intended to be released as open source in the future.

When making changes, treat the codebase as public-facing software:

- Do not add secrets, private tokens, machine-specific credentials, or personal paths.
- Keep implementation choices explainable to outside contributors.
- Prefer clear, maintainable code over quick local-only fixes.
- Preserve compatibility with normal Windows desktop environments unless a change explicitly targets a narrower setup.
- Document user-visible limitations, especially around display drivers, game fullscreen modes, HDR, NVIDIA-specific behavior, or Windows gamma APIs.

## Build And Runtime

- This is a WPF desktop app targeting `net10.0-windows`.
- Build with:

```powershell
dotnet build
```

- If the app is already running, `dotnet build` can fail because `brightnessControl.exe` or `brightnessControl.dll` is locked by the running process. Close the tray app first, or build to a separate output directory:

```powershell
dotnet build -o .\bin\BuildCheck
```

## Coding Guidelines

- Keep changes scoped to the requested behavior.
- Avoid unrelated refactors.
- Use nullable-aware C# and keep warnings clean.
- Keep UI text consistent and understandable for Korean users where the current UI is Korean-facing.
- Prefer explicit logging for profile detection, display target selection, color profile application, verification, and restore behavior.
- Do not silently swallow failures when applying or restoring display settings. Log enough detail for users to understand whether the app detected the game and whether Windows accepted the color change.

## Display And Color Behavior

- Color profile application is sensitive to Windows display APIs, GPU drivers, HDR, and exclusive fullscreen games.
- NVIDIA support uses public NVAPI scanout intensity APIs for brightness/contrast where available, with Windows gamma ramp as fallback.
- Public NVAPI scanout APIs do not map one-to-one to NVIDIA App sliders, so do not rely on NVIDIA App UI values as verification. Verify through app logs/API return state and visible output.
- NVIDIA-style neutral color defaults are brightness 100, contrast 100, gamma 1, digital vibrance 50, and hue 0.
- Color values are clamped to brightness 80-120, contrast 80-120, gamma 0.30-2.80, digital vibrance 0-100, and hue 0-359.
- When adding or changing display/color code, preserve restore behavior so original display settings can be recovered on game exit, manual restore, app shutdown, and unhandled exceptions.
- If an API reports success, verify the applied state when possible and log mismatches.
- Keep monitor targeting explicit. Color profiles use one global display target for the whole app, not per-profile display targets.
- Do not assume the primary display unless the global display target is `Primary`.

## Game Detection

- Game process detection is based on process names such as `Terraria.exe`.
- Foreground game focus is checked every 250 ms to reduce tab-switch restore/apply latency.
- Installed-game discovery is best-effort. Launcher libraries and install manifests vary, so discovery code should be conservative and resilient to missing folders, inaccessible paths, and unexpected manifests.
- Do not block the UI thread during long filesystem scans.

## Repository Hygiene

- Do not commit build outputs from `bin/` or `obj/`.
- Avoid committing generated local profile files such as `%APPDATA%\brightnessControl\profiles.json`.
- Keep public documentation accurate if behavior changes.
