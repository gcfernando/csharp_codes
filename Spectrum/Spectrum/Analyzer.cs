using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;

using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace Spectrum
{
    public delegate void OnChangeHandler(object obj, OnChangeEventArgs e);

    public class OnChangeEventArgs : EventArgs
    {
        public readonly List<byte> Spectrumdata;
        public OnChangeEventArgs(List<byte> spectrumdata) => Spectrumdata = spectrumdata;
    }

    public class Analyzer
    {
        public static event OnChangeHandler OnChange;

        private readonly int _size = 8192;
        private readonly int _lines = 82;

        private readonly DispatcherTimer _timer;
        private readonly float[] _fft;
        private readonly WASAPIPROC _process;

        private int _lastlevel;
        private int _hanctr;

        private readonly List<byte> _spectrumdata;
        private readonly OnChangeEventArgs _changeArgs;

        private bool _initialized = false;
        private List<Device> devices = null;

        private readonly int[] _bandEdges;

        public int SelectIndex { get; set; }

        public Analyzer()
        {
            BassNet.Registration("buddyknox@usa.org", "2X11841782815");

            _fft = new float[_size];

            _lastlevel = 0;
            _hanctr = 0;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(25)
            };
            _timer.Tick += timer_Tick;

            _process = new WASAPIPROC(Process);

            _spectrumdata = new List<byte>(_lines);
            _bandEdges = BuildBandsUpper(_lines, _size);

            // Reuse event args (removes per-tick allocation; same list reference as before)
            _changeArgs = new OnChangeEventArgs(_spectrumdata);

            _ = Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            _ = Bass.BASS_Init(-1, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

            _ = DeviceList();
            Enable(true);
        }

        private static int[] BuildBandsUpper(int lines, int size)
        {
            var upper = new int[lines];
            for (var x = 0; x < lines; x++)
            {
                var b1 = (int)Math.Pow(2, x * 10.0 / (lines - 1));
                if (b1 > size)
                {
                    b1 = size;
                }

                upper[x] = b1;
            }
            return upper;
        }

        private List<Device> DeviceList()
        {
            devices = new List<Device>();

            var count = BassWasapi.BASS_WASAPI_GetDeviceCount();

            for (var i = 0; i < count; i++)
            {
                var di = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);

                var flags = di.flags;

                var isEnabled = (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_ENABLED) != 0;
                var isInput = (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_INPUT) != 0;
                var isLoopback = (flags & BASSWASAPIDeviceInfo.BASS_DEVICE_LOOPBACK) != 0;

                if (isEnabled && isInput && isLoopback)
                {
                    devices.Add(new Device { Index = i, DeviceName = di.name });
                    SelectIndex = i;
                }
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

        private int Process(IntPtr buffer, int length, IntPtr user) => length;

        public void Free()
        {
            _ = BassWasapi.BASS_WASAPI_Free();
            _ = Bass.BASS_Free();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            Array.Clear(_fft, 0, _fft.Length);

            var ret = BassWasapi.BASS_WASAPI_GetData(
                _fft,
                (int)BASSData.BASS_DATA_FFT16384);

            if (ret < -1)
            {
                return;
            }

            var b0 = 0;
            var size = _size;
            var lines = _lines;

            var fft = _fft;
            var fftOffset = 1;
            var bandsUpper = _bandEdges;
            var spectrum = _spectrumdata;

            for (var x = 0; x < lines; x++)
            {
                var b1 = bandsUpper[x];
                if (b1 > size)
                {
                    b1 = size;
                }

                if (b1 <= b0)
                {
                    b1 = b0 + 1;
                }

                var sum = 0.0;
                var n = 0;

                for (; b0 < b1; b0++)
                {
                    var v = fft[fftOffset + b0];
                    sum += v * v;
                    n++;
                }

                var rms = n > 0 ? Math.Sqrt(sum / n) : 0.0;

                var y = (int)((Math.Sqrt(rms) * 3 * 255) - 4);

                if (y > 255)
                {
                    y = 255;
                }

                if (y < 0)
                {
                    y = 0;
                }

                spectrum.Add((byte)y);
            }

            OnEventChange(_changeArgs);

            var level = BassWasapi.BASS_WASAPI_GetLevel();
            if (level == _lastlevel && level != 0)
            {
                _hanctr++;
            }

            _lastlevel = level;

            spectrum.Clear();

            if (_hanctr > 3)
            {
                _hanctr = 0;
                Free();

                _ = Bass.BASS_Init(0, 48000, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

                _initialized = false;
                Enable(true);
            }
        }

        private static readonly object _sender = new object();

        public static void OnEventChange(OnChangeEventArgs e)
            => OnChange?.Invoke(_sender, e);
    }
}