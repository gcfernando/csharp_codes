using System;
using System.Threading;
using System.Threading.Tasks;
using Spectrum.Dsp;
using Xunit;

namespace Spectrum.Tests;

public class SampleHistoryTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _out;

    public SampleHistoryTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

    private static float[] Ramp(long startFrame, int frames, int channels)
        => Signals.Interleaved(frames, channels, (c, n) => ((startFrame + n) * 10) + c);

    [Fact]
    public void ReturnsLatestFramesOldestFirst_AcrossTheWrap()
    {
        var h = new SampleHistory(2, 100);
        long written = 0;
        foreach (var chunk in new[] { 30, 45, 60, 17 })
        {
            h.Write(Ramp(written, chunk, 2), chunk);
            written += chunk;
        }

        var dest = new float[80 * 2];
        Assert.True(h.TryCopyLatest(dest, 80, out var end));
        Assert.Equal(written, end);
        for (var i = 0; i < 80; i++)
        {
            Assert.Equal(((written - 80 + i) * 10) + 0, dest[2 * i]);
            Assert.Equal(((written - 80 + i) * 10) + 1, dest[(2 * i) + 1]);
        }
    }

    [Fact]
    public void ZeroFillsFramesThatWereNeverWritten()
    {
        var h = new SampleHistory(1, 64);
        h.Write(Ramp(0, 10, 1), 10);
        var dest = new float[32];
        dest.FillWith(99);
        Assert.True(h.TryCopyLatest(dest, 32, out _));
        for (var i = 0; i < 22; i++)
        {
            Assert.Equal(0f, dest[i]);
        }

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i * 10f, dest[22 + i]);
        }
    }

    [Fact]
    public void FramesBeforeAGap_AreExcludedFromTheSnapshot()
    {
        // 50 frames before a pause, then 10 frames after it resumed at frame 50.
        var h = new SampleHistory(1, 64);
        h.Write(Ramp(0, 50, 1), 50);
        h.Write(Ramp(50, 10, 1), 10);
        var dest = new float[32];
        dest.FillWith(99);

        Assert.True(h.TryCopyLatest(dest, 32, out var end, discardBeforeFrame: 50));
        Assert.Equal(60, end);
        for (var i = 0; i < 22; i++)
        {
            Assert.Equal(0f, dest[i]); // pre-gap audio replaced by silence
        }

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal((50 + i) * 10f, dest[22 + i]);
        }

        // Once enough new audio has arrived the gap no longer affects the window.
        h.Write(Ramp(60, 40, 1), 40);
        Assert.True(h.TryCopyLatest(dest, 32, out _, discardBeforeFrame: 50));
        Assert.Equal(68 * 10f, dest[0]);
    }

    [Fact]
    public void OversizedWrite_KeepsOnlyTheNewestFrames()
    {
        var h = new SampleHistory(1, 16);
        h.Write(Ramp(0, 40, 1), 40);
        var dest = new float[16];
        Assert.True(h.TryCopyLatest(dest, 16, out var end));
        Assert.Equal(16, end); // only the frames that could be stored are counted
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal((24 + i) * 10f, dest[i]);
        }
    }

    [Fact]
    public async Task ConcurrentProducer_NeverYieldsATornSnapshot()
    {
        // Producer writes a strictly increasing frame counter; any snapshot the consumer accepts must be
        // a contiguous run of consecutive frames. A torn read would show a discontinuity.
        // The ring has only a small margin over the window so the unthrottled producer laps the reader often:
        // the torn-read validation must actually fire (rejected > 0) and every accepted snapshot must be clean.
        const int channels = 2, window = 4096, chunk = 480;
        // Capacity = window + one in-flight chunk: a snapshot is accepted only if the producer made no further
        // progress into the region during the copy.
        var h = new SampleHistory(channels, window + chunk);
        using var stop = new CancellationTokenSource();

        // Precomputed ramp so the producer runs at memcpy speed (a per-frame fill loop is too slow on x86 to ever
        // lap the reader). Frame value = frame index modulo the cycle; the cycle is a whole number of chunks.
        const int cycle = chunk * 136;
        var ramp = new float[cycle * channels];
        for (var i = 0; i < cycle; i++)
        {
            ramp[i * channels] = i;
            ramp[(i * channels) + 1] = -i;
        }

        var producer = Task.Run(() =>
        {
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(ramp, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                var basePtr = handle.AddrOfPinnedObject();
                var offset = 0;
                while (!stop.IsCancellationRequested)
                {
                    h.Write(IntPtr.Add(basePtr, offset * channels * sizeof(float)), chunk * channels * sizeof(float));
                    offset = (offset + chunk) % cycle;
                }
            }
            finally
            {
                handle.Free();
            }
        });

        var dest = new float[window * channels];
        int accepted = 0, rejected = 0;
        try
        {
            // Race for at least 2 s AND until there is enough evidence, so a fast machine does not stop early and a
            // loaded machine only makes the test slower (60 s cap).
            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started < TimeSpan.FromSeconds(2) || accepted < 200 || rejected == 0)
                   && DateTime.UtcNow - started < TimeSpan.FromSeconds(60))
            {
                if (!h.TryCopyLatest(dest, window, out var end))
                {
                    rejected++;
                    continue;
                }

                if (end < window)
                {
                    continue;
                }

                accepted++;
                for (var i = 1; i < window; i++)
                {
                    var prev = dest[(i - 1) * channels];
                    var cur = dest[i * channels];
                    if (!(cur == prev + 1 || (prev == cycle - 1 && cur == 0)) || dest[(i * channels) + 1] != -cur)
                    {
                        Assert.Fail($"torn snapshot at {i}: {prev} -> {cur}, right {dest[(i * channels) + 1]}");
                    }
                }
            }
        }
        finally
        {
            stop.Cancel(); // never leave the producer spinning, even when an assertion fails
        }

        await producer;
        _out.WriteLine($"accepted {accepted}, rejected {rejected}");
        Assert.True(accepted >= 200, $"only {accepted} snapshots accepted ({rejected} rejected) in 60 s");
        Assert.True(rejected > 0, "the producer never lapped the reader, so torn-read detection was not exercised");
    }
}

internal static class ArrayTestExtensions
{
    public static void FillWith(this float[] a, float value)
    {
        for (var i = 0; i < a.Length; i++)
        {
            a[i] = value;
        }
    }
}
