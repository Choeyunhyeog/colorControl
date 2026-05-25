# brightnessControl

Windows tray application that applies a display color profile while a registered game is the foreground window, then restores the original display settings when the game loses focus or exits.

## Features

- WPF desktop UI with system tray operation.
- Profile editor for game name, process name, brightness, contrast, gamma, Digital Vibrance, and hue values.
- Installed game scan for Steam libraries and common game folders.
- Foreground process monitoring with automatic apply, reapply, and restore.
- Global monitor target selection, including the primary display and detected Windows displays.
- JSON profile storage at `%APPDATA%\brightnessControl\profiles.json`.
- Gamma backup storage at `%APPDATA%\brightnessControl\gamma-backup.json` so a previous session can be restored on the next launch.
- Manual `Apply Now` and `Restore Original` actions in the main window.
- Tray menu actions for opening the app, restoring original settings, and exiting.
- NVIDIA NVAPI scanout path for supported brightness/contrast configurations, with Windows gamma ramp fallback.

## Requirements

- Windows 10 or Windows 11.
- .NET 10 SDK.
- Optional: NVIDIA driver with `nvapi64.dll` for the NVAPI scanout path.

The app still runs without NVIDIA support. In that case it uses the Windows `GetDeviceGammaRamp` / `SetDeviceGammaRamp` fallback for brightness, contrast, and gamma adjustments.

## Quick Start

Build the solution:

```powershell
dotnet build
```

Run the application:

```powershell
dotnet run --project .\brightnessControl\brightnessControl.csproj
```

Run the lightweight service tests:

```powershell
dotnet run --project .\tests\brightnessControl.ServiceTests\brightnessControl.ServiceTests.csproj
```

## Usage

1. Start the app.
2. Click `Scan Games` and add a detected game, or edit the generated `Notepad Test` profile.
3. Select the monitor target.
4. Set the profile values:
   - Brightness: `80` to `120`
   - Contrast: `80` to `120`
   - Gamma: `0.30` to `2.80`
   - Digital Vibrance: `0` to `100`
   - Hue: `0` to `359`
5. Enable the profile and click `Save`.
6. Bring the matching game window to the foreground.

When the game becomes the foreground window, the profile is applied. When focus moves away from the game or the game process exits, the app restores the original display settings.

For a safe local test, enable the `Notepad Test` profile, save, start `notepad.exe`, then switch focus away from Notepad or close it. The log panel should show detection, profile application, and restore activity.

## Safety And Recovery

The app backs up the current gamma ramp before applying a profile. Restore is attempted when:

- the monitored game loses foreground focus,
- the monitored game process exits,
- `Restore Original` is clicked,
- `Restore Original Settings` is selected from the tray menu,
- the app exits,
- an unhandled exception is observed,
- a persisted gamma backup is found on the next launch.

If display colors remain incorrect, reopen the app and click `Restore Original`. If that does not work, sign out/in or reboot Windows to force the display stack to reset.

## NVIDIA Notes

This project includes an NVAPI-based scanout implementation. It attempts to apply brightness and contrast through NVIDIA scanout intensity when supported by the active GPU, driver, and display configuration.

Important limitations:

- NVIDIA's public scanout functions do not expose the NVIDIA App or NVIDIA Control Panel Digital Vibrance slider.
- Hue is stored in profiles, but the current NVAPI path does not apply the NVIDIA App hue slider.
- If NVAPI initialization or application fails, the app falls back to the Windows gamma ramp path.
- NVAPI scanout restore resets touched displays to neutral/default values because the original scanout texture values are not exposed.

## Known Limitations

- Process detection is based on the foreground window process name.
- Only one foreground profile is active at a time.
- Gamma ramp APIs can fail or be overridden depending on HDR state, driver behavior, exclusive fullscreen mode, remote sessions, and Windows color management.
- Brightness and contrast in the Windows fallback are approximated through the gamma ramp.
- Installed game discovery is heuristic and may select a launcher or miss games in custom folders.
- The project currently uses a lightweight console-style service test project instead of a full test framework.

## Project Structure

```text
brightnessControl/
  Models/       Profile and display target models
  Services/     Gamma, NVAPI, profile, process monitor, recovery, tray, and game discovery services
  *.xaml        WPF application UI
tests/
  brightnessControl.ServiceTests/
    Program.cs  Lightweight profile persistence tests
```

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).

Third-party and runtime notices are documented in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
