# Spectrum — Windows Audio Spectrum Visualizer

A .NET Framework 4.8 Windows Forms application that captures your system's audio output in real time and renders it as an animated frequency spectrum using 83 vertical bars. Audio capture is handled by BASS and BassWASAPI via Windows WASAPI loopback.

## Features

- **83-bar frequency spectrum** covering 20 Hz – 20 kHz on a logarithmic scale, with a labelled frequency axis
- **Measured band levels** — each bar is the integrated power of its frequency band in dBFS (see *Measurement*)
- **8 visualization modes** selectable via `App.config`
- **Heat-map colour gradient** — green (low) → yellow (mid) → red (high)
- **Peak-hold markers** with configurable hold time and decay
- **Asymmetric ballistics** — fast attack, slower release per mode
- **Frame-rate independent animation** — ballistics use elapsed time; analysis runs about every 32 ms (a 25 ms timer at the 15.6 ms Windows timer resolution), display up to 60 fps
- **Silence detection** — bars fade out gracefully when no audio is playing
- **Auto device recovery** — restores capture automatically when the default audio device changes
- **Scaling layout** — bars and labels are laid out from the window's client size (the process does not declare per-monitor DPI awareness, so Windows scales it on high-DPI monitors)
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
msbuild Spectrum.sln -restore -p:RestorePackagesConfig=true -p:Configuration=Release
```

The output is placed in `Spectrum/bin/Release/`.

## Tests

`Spectrum.Tests` (xUnit, .NET Framework 4.8) validates the analysis with deterministic synthetic signals:
FFT correctness, band-plan geometry, a tone at every bar's centre, amplitude accuracy, band-boundary
and sweep behaviour, multi-tone independence, white/pink noise, stereo semantics (including anti-phase),
numerical safety, lock-free sample-history consistency under a concurrent producer, frame-rate
independent ballistics and end-to-end onset/release timing, instrument-like signals, and per-frame cost.

```bash
dotnet test Spectrum.Tests/bin/Release/net48/Spectrum.Tests.dll
# The application runs as a 32-bit process; to test under x86:
vstest.console.exe Spectrum.Tests/bin/Release/net48/Spectrum.Tests.dll /Platform:x86
```

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
    ├── Analyzer.cs                  # Device handling, WASAPI capture, analysis scheduling, publication
    ├── Dsp/                         # Measurement: FFT, band plan, band-power analyzer, dB scale, sample history
    ├── BarBallistics.cs             # Time-based attack / release / peak-hold math used by the bars
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

## Measurement

**What a bar shows.** Bar *i* is the power of the captured signal inside a fixed frequency band, in dB
relative to a full-scale sine (dBFS): a full-scale sine whose energy lies inside one band reads 0 dBFS.
The 83 bands are contiguous and logarithmically spaced (constant ratio ≈ 1/8.3 octave); band *i* is centred
at 20·1000^(i/82) Hz and its edges are the geometric midpoints to its neighbours, so every frequency between
19.2 Hz and 20.9 kHz belongs to exactly one bar. No frequency weighting, bass/treble boost or per-frame
normalization is applied, so pink noise reads flat and white noise rises 3 dB per octave.

**Pipeline.** WASAPI loopback (32-bit float) → lock-free sample history (written by the capture callback) →
every analysis tick (~32 ms): latest frames per channel → window-weighted DC removal → periodic Hann window → FFT →
one-sided bin power 2|X|²/(N·Σw²) (Parseval-normalized, so summed bins equal signal power) →
mean of left/right channel power (anti-phase content cannot cancel) → bin power integrated into each band
with fractional weights for bins that straddle a band edge → 10·log10 → fixed display range −72…0 dBFS mapped
linearly to bar height (levels below about −71.6 dBFS, the two lowest byte steps, are gated to an empty bar) → time-based presentation ballistics.

**Resolution.** Each band uses the shortest window that places at least 4 bins (the Hann main lobe) inside it:
≈85 ms above ~0.5 kHz, ≈171 ms around 0.3–0.5 kHz, ≈341 ms below (window lengths scale with the sample rate).
Short windows cut latency (a 1 kHz onset reaches 90 % in ~50 ms instead of ~150 ms with a single 341 ms
window); the long window keeps bass resolution. Below ~140 Hz the bars are narrower than the window's main
lobe (≈11.7 Hz at 48 kHz): a pure bass tone lights a small cluster of neighbouring bars, with the strongest
bar at the tone's frequency, and reads up to ~5 dB low in its own bar. This is a physical time/frequency limit, not something the display hides.

**Stereo.** The front left/right pair is analysed separately and powers are averaged: L = R reads like mono,
one channel alone reads −3 dB, and phase between channels never changes the level. On multichannel devices
only the front pair is measured.

**Timing (default Spectrum mode).** Attack: full-scale rise in 45 ms. Release: exponential, time constant
280 ms. Peak marker: 300 ms hold, then a linear fall, independent of the bar. Bricks use ±2 px (≈0.5 dB) hysteresis so a level
hovering at a brick boundary does not flicker (steady-level display error ≤ ±1.44 dB); the animation runs at ~64 fps. Other visual modes keep their
own presentation presets.

## FAQ

**The top bars are empty or much lower on some songs. Is something broken?**
No. Lossy encoders remove high frequencies: most MP3s are encoded with a low-pass at 15–19 kHz, so bars 80–82
(16.2–20.9 kHz) correctly show nothing for them (a scan of a 368-song MP3 library found 231 files cut at 15–16 kHz;
every empty top bar lay above the file's own cut-off). White or pink noise lights all 83 bars (a 20 kHz test tone lights bar 82).

**Why do bass bars move later than cymbals?**
Resolving bass needs a longer analysis window (≈341 ms below ~280 Hz versus ≈85 ms above ~560 Hz), so bass
bars trail treble bars by roughly 0.1 s. This is the time/frequency trade-off of any FFT analyzer.

## Technical Notes

- Device changes (plug/unplug, default device switch) are detected via `IMMNotificationClient` and trigger an automatic re-initialisation on the UI thread
- A hang detector monitors consecutive identical WASAPI levels and forces a device reset if the stream appears stuck
- Silence (zero level, or no new captured frames) publishes the floor; bars then decay with their release time
