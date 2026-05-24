# Spectrum — Windows Audio Spectrum Visualizer

A .NET Framework 4.8 Windows Forms application that captures your system's audio output in real time and renders it as an animated frequency spectrum using 83 vertical bars. Audio capture is handled by BASS and BassWASAPI via Windows WASAPI loopback.

## Features

- **83-bar frequency spectrum** covering 20 Hz – 20 kHz on a logarithmic scale
- **8 visualization modes** selectable via `App.config`
- **Heat-map colour gradient** — green (low) → yellow (mid) → red (high)
- **Peak-hold markers** with configurable hold time and decay
- **Asymmetric ballistics** — fast attack, slower release per mode
- **60 fps animation** via a 25 ms timer loop
- **Silence detection** — bars fade out gracefully when no audio is playing
- **Auto device recovery** — restores capture automatically when the default audio device changes
- **DPI-aware layout** — bars scale to fit the window on any monitor DPI
- **F12 toggle** — toggle always-on-top
- **Single-instance** — uses a named mutex to prevent multiple instances

## Visualization Modes

Set the `Mode` key in `App.config`:

| Mode | Best For |
|---|---|
| `Spectrum` (default) | All-around; real-time frequency analysis |
| `Bricks` | Retro block aesthetic; 80s/Synthwave music |
| `LED` | Professional peak meter; recording studios |
| `Dots` | Smooth minimal style; ambient music |
| `Wave` | Flowing organic motion; lo-fi / chill-hop |
| `Pulse` | Breathing hypnotic effect; meditation / downtempo |
| `Center` | Energy from centre; orchestral / cinematic |
| `Mirror` | Symmetrical balance; EDM / house music |

## Configuration

Edit `App.config` in the project directory:

```xml
<appSettings>
  <add key="Mode" value="Spectrum"/>
</appSettings>
```

## Requirements

- Windows OS
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- Visual Studio 2022 (or MSBuild)
- The native DLLs `bass.dll` and `basswasapi.dll` (included in `Ref_Files/`)

## Build

Open `Spectrum.sln` in Visual Studio and build (`Ctrl+Shift+B`), or:

```bash
msbuild Spectrum.sln /p:Configuration=Release
```

The output is placed in `Spectrum/bin/Release/`.

## Running

Run `Spectrum.exe` from the build output directory. The application automatically selects the Windows default audio output device (WASAPI loopback) and begins capturing.

## Dependencies

| Library | Version | Source |
|---|---|---|
| `Bass.Net` | 2.4.11.1 | Included in `Ref_Files/` |
| `NAudio` | 1.7.3 | Included in `Ref_Files/` |
| `bass.dll` | — | Native BASS audio library |
| `basswasapi.dll` | — | Native BassWASAPI extension |

## Project Structure

```
Spectrum/
└── Spectrum/
    ├── Analyzer.cs                  # Audio capture, FFT processing, bar data calculation
    ├── FormAudioSpectrum.cs         # Main form — renders bars, handles events
    ├── FormAudioSpectrum.Designer.cs
    ├── VerticalProgressBar.cs       # Custom control — one animated bar
    ├── Ambiance Theme.cs            # Visual theme applied to the form
    ├── Device.cs                    # WASAPI device descriptor
    ├── Taskbar.cs                   # Windows taskbar progress state helper
    ├── Program.cs                   # Entry point with single-instance mutex
    ├── App.config                   # Mode configuration
    ├── Ref_Files/                   # Bass.Net.dll, NAudio.dll, bass.dll, basswasapi.dll
    └── Spectrum.csproj
```

## Technical Notes

- FFT size: 16 384 points (`BASS_DATA_FFT16384`)
- Bar values are computed as the RMS of each logarithmic frequency band, smoothed with a 0.20 factor
- Device changes (plug/unplug, default device switch) are detected via `IMMNotificationClient` and trigger an automatic re-initialisation on the UI thread
- A hang detector monitors consecutive identical WASAPI levels and forces a device reset if the stream appears stuck
