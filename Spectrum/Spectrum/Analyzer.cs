using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

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

    private const int FFT_LEN = 16384;
    private const int FFT_HALF_BINS = FFT_LEN / 2;
    private const int FFT_SIZE = FFT_HALF_BINS;
    private const int LINES = 83;

    private const float MIN_HZ = 20f;
    private const float MAX_HZ = 20000f;

    private const int TIMER_INTERVAL_MS = 25;
    private const int HANG_THRESHOLD = 8;
    private const int SILENCE_FRAMES_REQUIRED = 4;

    private const double RMS_MULTIPLIER = 3.0 * 255.0;
    private const double RMS_OFFSET = 4.0;

    private const float SMOOTHING_FACTOR = 0.20f;

    private const float SILENCE_FADE_MULT = 0.80f;
    private const float SILENCE_SNAP_TO_ZERO = 0.50f;

    private readonly Timer _timer;
    private readonly float[] _fft;
    private int[] _bandEdges;
    private readonly float[] _smoothedSpectrum;
    private readonly byte[] _spectrumData;

    private readonly byte[] _fireData;
    private readonly OnChangeEventArgs _fireEventArgs;

    private readonly WASAPIPROC _process;
    private static readonly object _sender = new();

    private readonly SynchronizationContext _syncContext;

    private NAudio.CoreAudioApi.MMDeviceEnumerator _mmEnumerator;
    private DeviceNotificationClient _deviceNotificationClient;

    private int _lastLevel;
    private int _hangCounter;
    private int _consecutiveZeroLevels;

    private bool _initialized;
    private volatile bool _disposed;
    private bool _silenceMode;
    private volatile bool _recovering; // set while device recovery is pending on UI thread

    private int _sampleRate = 48000; // updated from WASAPI info after init

    public int SelectIndex { get; set; }

    public Analyzer()
    {
        BassNet.Registration("buddyknox@usa.org", "2X11841782815");

        _syncContext = SynchronizationContext.Current;

        _fft = new float[FFT_SIZE];
        _spectrumData = new byte[LINES];
        _smoothedSpectrum = new float[LINES];

        _fireData = new byte[LINES];
        _fireEventArgs = new OnChangeEventArgs(_fireData);

        _bandEdges = BuildBandsUpper_LogHz(LINES, FFT_SIZE, _sampleRate, MIN_HZ, MAX_HZ);

        _process = Process;

        _timer = new Timer { Interval = TIMER_INTERVAL_MS, AutoReset = false };
        _timer.Elapsed += TimerTick;

        _ = Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, 0);
        _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

        _ = DeviceList();
        Enable(true);

        try
        {
            _mmEnumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            _deviceNotificationClient = new DeviceNotificationClient(OnAudioDeviceChanged);
            _mmEnumerator.RegisterEndpointNotificationCallback(_deviceNotificationClient);
        }
        catch { }
    }

    private void OnAudioDeviceChanged()
    {
        if (_disposed || _recovering) return;
        _recovering = true;

        var ctx = _syncContext;
        if (ctx != null)
        {
            ctx.Post(_ =>
            {
                if (_disposed) { _recovering = false; return; }
                _timer.Stop();
                Free();
                _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                _initialized = false;
                _ = DeviceList();
                _recovering = false;
                Enable(true);
            }, null);
        }
        else
        {
            _recovering = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int[] BuildBandsUpper_LogHz(int lines, int maxBin, int sampleRate, float minHz, float maxHz)
    {
        var upper = new int[lines];

        var nyquist = sampleRate * 0.5f;
        var lo = Math.Max(1f, minHz);
        var hi = Math.Max(lo + 1f, Math.Min(maxHz, nyquist));

        var logLo = Math.Log10(lo);
        var logHi = Math.Log10(hi);
        var denom = Math.Max(1, lines - 1);

        var prev = 0;

        for (var x = 0; x < lines; x++)
        {
            var t = (double)x / denom;
            var hz = Math.Pow(10.0, logLo + ((logHi - logLo) * t));

            var bin = (int)Math.Round(hz * FFT_LEN / sampleRate);

            if (bin < 1) bin = 1;
            if (bin > maxBin) bin = maxBin;
            if (bin <= prev) bin = Math.Min(prev + 1, maxBin);

            upper[x] = bin;
            prev = bin;
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
        {
            SelectIndex = device.Index;
        }

        return devices;
    }

    private void Enable(bool value)
    {
        if (_disposed)
        {
            return;
        }

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

                try
                {
                    var info = BassWasapi.BASS_WASAPI_GetInfo();
                    if (info.freq > 0)
                    {
                        _sampleRate = info.freq;
                    }
                }
                catch
                {
                    _sampleRate = 48000;
                }

                _bandEdges = BuildBandsUpper_LogHz(LINES, FFT_SIZE, _sampleRate, MIN_HZ, MAX_HZ);

                _initialized = true;
            }

            _ = BassWasapi.BASS_WASAPI_Start();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _ = BassWasapi.BASS_WASAPI_Stop(true);
            _initialized = false;
            Free();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Process(IntPtr buffer, int length, IntPtr user) => length;

    public void Free()
    {
        if (_disposed)
        {
            return;
        }

        _ = BassWasapi.BASS_WASAPI_Free();
        _ = Bass.BASS_Free();
    }

    private void TimerTick(object sender, ElapsedEventArgs e)
    {
        if (_disposed || _recovering) return;

        try
        {
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
        finally
        {
            if (!_disposed && !_recovering)
            {
                try { _timer.Start(); }
                catch (ObjectDisposedException) { }
            }
        }
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
            {
                _smoothedSpectrum[i] = 0;
            }

            var v = (byte)_smoothedSpectrum[i];
            _spectrumData[i] = v;

            if (v != 0)
            {
                anyNonZero = true;
            }
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
            if (b1 >= FFT_SIZE)
            {
                b1 = FFT_SIZE - 1;
            }

            if (b1 <= b0)
            {
                b1 = b0 + 1;
            }

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

            _smoothedSpectrum[x] = _smoothedSpectrum[x] * SMOOTHING_FACTOR
                                 + clamped * (1.0f - SMOOTHING_FACTOR);

            var finalValue = (byte)_smoothedSpectrum[x];
            _spectrumData[x] = finalValue;

            if (finalValue > 0)
            {
                hasSignal = true;
            }
        }

        if (hasSignal || !_silenceMode)
        {
            FireOnChange();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FireOnChange()
    {
        Buffer.BlockCopy(_spectrumData, 0, _fireData, 0, LINES);
        OnChange?.Invoke(_sender, _fireEventArgs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleDeviceHang(int level)
    {
        if (level == _lastLevel && level != 0)
        {
            _hangCounter++;
        }
        else
        {
            _hangCounter = 0;
        }

        _lastLevel = level;

        if (_hangCounter > HANG_THRESHOLD)
        {
            _hangCounter = 0;
            _consecutiveZeroLevels = 0;
            _recovering = true;

            var ctx = _syncContext;
            if (ctx != null)
            {
                ctx.Post(_ =>
                {
                    if (_disposed) { _recovering = false; return; }
                    Free();
                    _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                    _initialized = false;
                    _recovering = false;
                    Enable(true);
                }, null);
            }
            else
            {
                Free();
                _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                _initialized = false;
                _recovering = false;
                Enable(true);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_mmEnumerator != null)
        {
            try
            {
                if (_deviceNotificationClient != null)
                    _mmEnumerator.UnregisterEndpointNotificationCallback(_deviceNotificationClient);
            }
            catch { }
            _mmEnumerator = null;
        }

        _timer.Stop();
        _timer.Elapsed -= TimerTick;
        _timer.Dispose();

        // Call BASS cleanup directly — Free() checks _disposed and would return early here
        _ = BassWasapi.BASS_WASAPI_Free();
        _ = Bass.BASS_Free();

        OnChange = null;
    }

    private sealed class DeviceNotificationClient : NAudio.CoreAudioApi.Interfaces.IMMNotificationClient
    {
        private readonly Action _onDefaultChanged;

        public DeviceNotificationClient(Action onDefaultChanged)
            => _onDefaultChanged = onDefaultChanged;

        public void OnDefaultDeviceChanged(
            NAudio.CoreAudioApi.DataFlow flow,
            NAudio.CoreAudioApi.Role role,
            string defaultDeviceId)
        {
            if (flow == NAudio.CoreAudioApi.DataFlow.Render &&
                role == NAudio.CoreAudioApi.Role.Multimedia)
            {
                _onDefaultChanged();
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, NAudio.CoreAudioApi.DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, NAudio.CoreAudioApi.PropertyKey key) { }
    }
}