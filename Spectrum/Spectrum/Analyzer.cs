using System;
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

    public class OnChangeEventArgs : EventArgs
    {
        public IReadOnlyList<byte> Spectrumdata { get; }
        public OnChangeEventArgs(List<byte> spectrumdata) => Spectrumdata = spectrumdata.AsReadOnly();
    }

    public class Analyzer
    {
        public static event OnChangeHandler OnChange;

        private const int _size = 8192;
        private const int _lines = 82;
        private const int _timerIntervalMs = 25;
        private const int _hangThreshold = 3;
        private const double _rmsMultiplier = 3.0 * 255.0;
        private const double _rmsOffset = 4.0;

        private readonly DispatcherTimer _timer;
        private readonly float[] _fft;
        private readonly WASAPIPROC _process;
        private readonly int[] _bandEdges;
        private readonly List<byte> _spectrumdata;
        private readonly OnChangeEventArgs _changeArgs;
        private static readonly object _sender = new object();

        private int _lastlevel;
        private int _hanctr;
        private bool _initialized;

        public int SelectIndex { get; set; }

        public Analyzer()
        {
            BassNet.Registration("buddyknox@usa.org", "2X11841782815");

            _fft = new float[_size];
            _spectrumdata = new List<byte>(_lines);
            _bandEdges = BuildBandsUpper(_lines, _size);
            _changeArgs = new OnChangeEventArgs(_spectrumdata);
            _process = new WASAPIPROC(Process);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_timerIntervalMs)
            };
            _timer.Tick += TimerTick;

            _ = Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            _ = Bass.BASS_Init(-1, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

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
            var _devices = new List<Device>(count);

            for (var i = 0; i < count; i++)
            {
                var di = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                var flags = di.flags;

                if ((flags & BASSWASAPIDeviceInfo.BASS_DEVICE_ENABLED) != 0 &&
                    (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_INPUT) != 0 &&
                    (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_LOOPBACK) != 0)
                {
                    _devices.Add(new Device { Index = i, DeviceName = di.name });
                }
            }

            // Prefer Headset, fallback to Speakers
            var device = _devices.FirstOrDefault(d => d.DeviceName.Contains("Headphones"))
                      ?? _devices.FirstOrDefault(d => d.DeviceName.Contains("Speakers"));

            if (device != null)
            {
                SelectIndex = device.Index;
            }

            return _devices;
        }

        private void Enable(bool value)
        {
            if (value)
            {
                if (!_initialized)
                {
                    _ = BassWasapi.BASS_WASAPI_GetDeviceInfo(SelectIndex);
                    _ = BassWasapi.BASS_WASAPI_Init(
                        SelectIndex,
                        0,
                        0,
                        BASSWASAPIInit.BASS_WASAPI_AUTOFORMAT | BASSWASAPIInit.BASS_WASAPI_BUFFER,
                        1f,
                        0.05f,
                        _process,
                        IntPtr.Zero);

                    _initialized = true;
                }

                _ = BassWasapi.BASS_WASAPI_Start();
                Thread.Sleep(250);
                _timer.IsEnabled = true;
            }
            else
            {
                _ = BassWasapi.BASS_WASAPI_Stop(true);
                _timer.IsEnabled = false;
                _initialized = false;
                Free();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Process(IntPtr buffer, int length, IntPtr user) => length;

        public void Free()
        {
            _ = BassWasapi.BASS_WASAPI_Free();
            _ = Bass.BASS_Free();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            Array.Clear(_fft, 0, _fft.Length);

            var ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)BASSData.BASS_DATA_FFT16384);
            if (ret < -1)
            {
                return;
            }

            ProcessFFTData();

            var level = BassWasapi.BASS_WASAPI_GetLevel();
            HandleDeviceHang(level);

            _spectrumdata.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessFFTData()
        {
            var b0 = 0;
            const int fftOffset = 1;

            for (var x = 0; x < _lines; x++)
            {
                var b1 = Math.Min(_bandEdges[x], _size);
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
                var y = (int)((Math.Sqrt(rms) * _rmsMultiplier) - _rmsOffset);

                // Clamp for .NET 4.8
                if (y > 255)
                {
                    y = 255;
                }

                if (y < 0)
                {
                    y = 0;
                }

                _spectrumdata.Add((byte)y);
            }

            OnChange?.Invoke(_sender, _changeArgs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleDeviceHang(int level)
        {
            if (level == _lastlevel && level != 0)
            {
                _hanctr++;
            }
            else
            {
                _hanctr = 0;
            }

            _lastlevel = level;

            if (_hanctr > _hangThreshold)
            {
                _hanctr = 0;
                Free();
                _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                _initialized = false;
                Enable(true);
            }
        }
    }
}