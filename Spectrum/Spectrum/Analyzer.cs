// =============================== Analyzer.cs ===============================
// Developed by Gehan Fernando (optimized + hardened)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace Spectrum;

public delegate void OnChangeHandler(object obj, OnChangeEventArgs e);

public sealed class OnChangeEventArgs : EventArgs
{
    public IReadOnlyList<byte> Spectrumdata { get; }
    public OnChangeEventArgs(byte[] spectrumdata) => Spectrumdata = spectrumdata;
}

public sealed class Analyzer : IDisposable
{
    public static event OnChangeHandler OnChange;

    private const int FFT_SIZE = 8192;
    private const int LINES = 83;

    private const int TIMER_INTERVAL_MS = 25; // ~40fps
    private const int HANG_THRESHOLD = 3;
    private const int SILENCE_FRAMES_REQUIRED = 4;

    private const double RMS_MULTIPLIER = 3.0 * 255.0;
    private const double RMS_OFFSET = 4.0;

    // Analyzer-side smoothing
    private const float SMOOTHING_FACTOR = 0.45f;

    // Silence fade characteristics
    private const float SILENCE_FADE_MULT = 0.80f; // per tick
    private const float SILENCE_SNAP_TO_ZERO = 0.50f;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly float[] _fft;
    private readonly int[] _bandEdges;
    private readonly float[] _smoothedSpectrum;
    private readonly byte[] _spectrumData;

    private readonly WASAPIPROC _process;
    private static readonly object _sender = new();

    private int _lastLevel;
    private int _hangCounter;
    private int _consecutiveZeroLevels;

    private bool _initialized;
    private bool _disposed;
    private bool _silenceMode;

    public int SelectIndex { get; set; }

    public Analyzer()
    {
        BassNet.Registration("buddyknox@usa.org", "2X11841782815");

        _fft = new float[FFT_SIZE];
        _spectrumData = new byte[LINES];
        _smoothedSpectrum = new float[LINES];
        _bandEdges = BuildBandsUpper(LINES, FFT_SIZE);

        _process = Process;

        _timer = new System.Windows.Forms.Timer
        {
            Interval = TIMER_INTERVAL_MS,
            Enabled = false
        };
        _timer.Tick += TimerTick;

        _ = Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, 0);
        _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

        _ = DeviceList();
        Enable(true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] BuildBandsUpper(int lines, int size)
    {
        var upper = new int[lines];
        var multiplier = 10.0 / (lines - 1);

        for (var x = 0; x < lines; x++)
        {
            var b1 = (int)Math.Pow(2, x * multiplier);
            upper[x] = Math.Min(b1, size);
        }

        return upper;
    }

    private List<Device> DeviceList()
    {
        var count = BassWasapi.BASS_WASAPI_GetDeviceCount();
        var devices = new List<Device>(Math.Max(0, count));

        for (var i = 0; i < count; i++)
        {
            var di = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
            var flags = di.flags;

            if ((flags & BASSWASAPIDeviceInfo.BASS_DEVICE_ENABLED) != 0 &&
                (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_INPUT) != 0 &&
                (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_LOOPBACK) != 0)
            {
                devices.Add(new Device { Index = i, DeviceName = di.name });
            }
        }

        var device =
            devices.FirstOrDefault(d => d.DeviceName.IndexOf("Headphones", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? devices.FirstOrDefault(d => d.DeviceName.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? devices.FirstOrDefault(d => d.DeviceName.IndexOf("Speakers", StringComparison.OrdinalIgnoreCase) >= 0)
            ?? devices.FirstOrDefault();

        if (device != null)
            SelectIndex = device.Index;

        return devices;
    }

    private void Enable(bool value)
    {
        if (_disposed) return;

        if (value)
        {
            if (!_initialized)
            {
                _ = BassWasapi.BASS_WASAPI_GetDeviceInfo(SelectIndex);

                var success = BassWasapi.BASS_WASAPI_Init(
                    SelectIndex,
                    0,
                    0,
                    BASSWASAPIInit.BASS_WASAPI_AUTOFORMAT | BASSWASAPIInit.BASS_WASAPI_BUFFER,
                    1f,
                    0.05f,
                    _process,
                    IntPtr.Zero);

                if (!success)
                {
                    var error = Bass.BASS_ErrorGetCode();
                    System.Diagnostics.Debug.WriteLine($"WASAPI Init failed: {error}");
                    return;
                }

                _initialized = true;
            }

            _ = BassWasapi.BASS_WASAPI_Start();
            Thread.Sleep(250);
            _timer.Enabled = true;
        }
        else
        {
            _timer.Enabled = false;
            _ = BassWasapi.BASS_WASAPI_Stop(true);
            _initialized = false;
            Free();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Process(IntPtr buffer, int length, IntPtr user) => length;

    public void Free()
    {
        if (_disposed) return;

        _ = BassWasapi.BASS_WASAPI_Free();
        _ = Bass.BASS_Free();
    }

    private void TimerTick(object sender, EventArgs e)
    {
        if (_disposed) return;

        // No Array.Clear: BASS fills the buffer
        var ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)BASSData.BASS_DATA_FFT16384);

        if (ret < 0)
        {
            FadeSilenceAndFire();
            return;
        }

        var level = BassWasapi.BASS_WASAPI_GetLevel();

        if (level == 0)
        {
            _consecutiveZeroLevels++;
            if (_consecutiveZeroLevels >= SILENCE_FRAMES_REQUIRED)
            {
                FadeSilenceAndFire();
                HandleDeviceHang(level);
                return;
            }
        }
        else
        {
            _consecutiveZeroLevels = 0;
            _silenceMode = false;
        }

        ProcessFFTDataAndFire();
        HandleDeviceHang(level);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FadeSilenceAndFire()
    {
        _silenceMode = true;

        var anyNonZero = false;

        for (var i = 0; i < LINES; i++)
        {
            _smoothedSpectrum[i] *= SILENCE_FADE_MULT;

            if (_smoothedSpectrum[i] < SILENCE_SNAP_TO_ZERO)
                _smoothedSpectrum[i] = 0;

            var v = (byte)_smoothedSpectrum[i];
            _spectrumData[i] = v;

            if (v != 0) anyNonZero = true;
        }

        FireOnChange();

        if (!anyNonZero)
        {
            _silenceMode = false;
            _consecutiveZeroLevels = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessFFTDataAndFire()
    {
        var b0 = 0;
        const int fftOffset = 1;

        var hasSignal = false;

        for (var x = 0; x < LINES; x++)
        {
            var b1 = _bandEdges[x];
            if (b1 > FFT_SIZE) b1 = FFT_SIZE;
            if (b1 <= b0) b1 = b0 + 1;

            var sum = 0.0;
            var n = b1 - b0;

            for (var i = b0; i < b1; i++)
            {
                var v = _fft[fftOffset + i];
                sum += v * v;
            }

            b0 = b1;

            var rms = n > 0 ? Math.Sqrt(sum / n) : 0.0;
            var rawValue = (int)((Math.Sqrt(rms) * RMS_MULTIPLIER) - RMS_OFFSET);

            var clamped = rawValue > 255 ? 255 : (rawValue < 0 ? 0 : rawValue);

            _smoothedSpectrum[x] = (_smoothedSpectrum[x] * SMOOTHING_FACTOR) +
                                   (clamped * (1.0f - SMOOTHING_FACTOR));

            var finalValue = (byte)_smoothedSpectrum[x];
            _spectrumData[x] = finalValue;

            if (finalValue > 0) hasSignal = true;
        }

        if (hasSignal || !_silenceMode)
            FireOnChange();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FireOnChange()
    {
        // 83 bytes: allocation is fine and safe (no pooling risk with events)
        var data = new byte[LINES];
        Buffer.BlockCopy(_spectrumData, 0, data, 0, LINES);

        OnChange?.Invoke(_sender, new OnChangeEventArgs(data));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDeviceHang(int level)
    {
        if (level == _lastLevel && level != 0)
            _hangCounter++;
        else
            _hangCounter = 0;

        _lastLevel = level;

        if (_hangCounter > HANG_THRESHOLD)
        {
            _hangCounter = 0;
            _consecutiveZeroLevels = 0;

            Free();
            _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            _initialized = false;
            Enable(true);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _timer.Enabled = false;
        _timer.Tick -= TimerTick;
        _timer.Dispose();

        Free();

        // If you want standard event behavior, remove this line.
        OnChange = null;
    }
}
