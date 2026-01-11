using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

using Un4seen.Bass;
using Un4seen.BassWasapi;

// Developed by Gehan Fernando

namespace Spectrum
{
    public delegate void OnChangeHandler(object obj, OnChangeEventArgs e);

    public sealed class OnChangeEventArgs : EventArgs
    {
        public IReadOnlyList<byte> Spectrumdata { get; }
        public OnChangeEventArgs(byte[] spectrumdata) => Spectrumdata = spectrumdata;
    }

    public sealed class Analyzer : IDisposable
    {
        public static event OnChangeHandler OnChange;

        // Configuration constants
        private const int SIZE = 8192;
        private const int LINES = 83;
        private const int TIMER_INTERVAL_MS = 25;
        private const int HANG_THRESHOLD = 3;
        private const double RMS_MULTIPLIER = 3.0 * 255.0;
        private const double RMS_OFFSET = 4.0;
        private const int SILENCE_THRESHOLD = 100;
        private const int SILENCE_FRAMES_REQUIRED = 4;
        private const float SMOOTHING_FACTOR = 0.65f;

        // Thread-safe data structures
        private readonly DispatcherTimer _timer;
        private readonly float[] _fft;
        private readonly WASAPIPROC _process;
        private readonly int[] _bandEdges;
        private readonly byte[] _spectrumData;
        private readonly byte[] _previousSpectrum;
        private readonly float[] _smoothedSpectrum;
        private readonly ConcurrentQueue<byte[]> _dataPool;
        private static readonly object _sender = new object();

        // State tracking
        private int _lastLevel;
        private int _hangCounter;
        private int _silenceCounter;
        private bool _initialized;
        private bool _disposed;
        private bool _isSilent;
        private int _consecutiveZeroLevels;

        public int SelectIndex { get; set; }

        public Analyzer()
        {
            BassNet.Registration("buddyknox@usa.org", "2X11841782815");

            // Pre-allocate all buffers
            _fft = new float[SIZE];
            _spectrumData = new byte[LINES];
            _previousSpectrum = new byte[LINES];
            _smoothedSpectrum = new float[LINES];
            _bandEdges = BuildBandsUpper(LINES, SIZE);
            _dataPool = new ConcurrentQueue<byte[]>();
            _process = Process;

            // Pre-populate pool with reusable arrays
            for (var i = 0; i < 4; i++)
            {
                _dataPool.Enqueue(new byte[LINES]);
            }

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(TIMER_INTERVAL_MS)
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
            var devices = new List<Device>(count);

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

            var device = devices.FirstOrDefault(d => d.DeviceName.Contains("Headphones"))
                      ?? devices.FirstOrDefault(d => d.DeviceName.Contains("Speakers"));

            if (device != null)
            {
                SelectIndex = device.Index;
            }

            return devices;
        }

        private void Enable(bool value)
        {
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
                _timer.IsEnabled = true;
            }
            else
            {
                _timer.IsEnabled = false;
                _ = BassWasapi.BASS_WASAPI_Stop(true);
                _initialized = false;
                Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Process(IntPtr buffer, int length, IntPtr user) => length;

        public void Free()
        {
            if (!_disposed)
            {
                _ = BassWasapi.BASS_WASAPI_Free();
                _ = Bass.BASS_Free();
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            // Clear FFT buffer efficiently
            Array.Clear(_fft, 0, SIZE);

            var ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)BASSData.BASS_DATA_FFT16384);

            // Handle errors and silence detection
            if (ret < 0)
            {
                HandleSilence();
                return;
            }

            var level = BassWasapi.BASS_WASAPI_GetLevel();

            // Detect complete silence (music stopped)
            if (level == 0)
            {
                _consecutiveZeroLevels++;
                if (_consecutiveZeroLevels >= SILENCE_FRAMES_REQUIRED)
                {
                    HandleSilence();
                    HandleDeviceHang(level);
                    return;
                }
            }
            else
            {
                _consecutiveZeroLevels = 0;
                _isSilent = false;
            }

            ProcessFFTData();
            HandleDeviceHang(level);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleSilence()
        {
            if (_isSilent)
            {
                return;
            }

            _isSilent = true;

            // Gradually fade out to prevent flickering
            for (var i = 0; i < LINES; i++)
            {
                _smoothedSpectrum[i] *= 0.5f;
                _spectrumData[i] = (byte)Math.Max(0, (int)_smoothedSpectrum[i]);
            }

            FireOnChange();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessFFTData()
        {
            var b0 = 0;
            const int fftOffset = 1;
            var hasSignal = false;

            for (var x = 0; x < LINES; x++)
            {
                var b1 = Math.Min(_bandEdges[x], SIZE);
                if (b1 <= b0)
                {
                    b1 = b0 + 1;
                }

                var sum = 0.0;
                var n = b1 - b0;

                // Vectorizable loop - compiler can optimize this
                for (var i = b0; i < b1; i++)
                {
                    var v = _fft[fftOffset + i];
                    sum += v * v;
                }

                b0 = b1;

                var rms = n > 0 ? Math.Sqrt(sum / n) : 0.0;
                var rawValue = (int)((Math.Sqrt(rms) * RMS_MULTIPLIER) - RMS_OFFSET);

                // Clamp
                var clampedValue = rawValue > 255 ? 255 : (rawValue < 0 ? 0 : rawValue);

                // Apply exponential smoothing to reduce flickering
                _smoothedSpectrum[x] = (_smoothedSpectrum[x] * SMOOTHING_FACTOR) +
                                       (clampedValue * (1.0f - SMOOTHING_FACTOR));

                var finalValue = (byte)_smoothedSpectrum[x];
                _spectrumData[x] = finalValue;

                if (finalValue > 0)
                {
                    hasSignal = true;
                }
            }

            // Only fire event if there's actual signal
            if (hasSignal || !_isSilent)
            {
                FireOnChange();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FireOnChange()
        {
            // Try to reuse pooled array
            if (!_dataPool.TryDequeue(out var data))
            {
                data = new byte[LINES];
            }

            // Fast block copy
            Buffer.BlockCopy(_spectrumData, 0, data, 0, LINES);

            var args = new OnChangeEventArgs(data);
            OnChange?.Invoke(_sender, args);

            // Return to pool for reuse
            if (_dataPool.Count < 8)
            {
                _dataPool.Enqueue(data);
            }
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
                _silenceCounter = 0;

                Free();
                _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                _initialized = false;
                Enable(true);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.IsEnabled = false;
            _timer.Tick -= TimerTick;

            Free();

            OnChange = null;
        }
    }
}