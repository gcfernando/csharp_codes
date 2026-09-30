using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spectrum.Dsp;

/// <summary>
/// Single-producer / single-consumer history of the most recent interleaved float frames.
///
/// The producer (audio callback) never blocks, never allocates and never waits for the consumer:
/// it copies into a fixed ring and then advances a published frame counter. The consumer copies the
/// latest frames out and validates afterwards that the producer did not overwrite the region during
/// the copy (seqlock-style); a torn snapshot is retried rather than published.
/// </summary>
internal sealed class SampleHistory
{
    private const int MaxSnapshotAttempts = 4;

    private readonly float[] _ring;
    private readonly int _capacityFrames;

    // Frames fully written and visible to the consumer. 64-bit counters are only accessed through Interlocked,
    // which is atomic (and a full fence) in 32-bit processes too; the application runs as a 32-bit process.
    private long _committedFrames;

    // Upper bound of frames the producer may currently be writing (set before the copy starts).
    private long _reservedFrames;

    public SampleHistory(int channels, int capacityFrames)
    {
        if (channels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        if (capacityFrames < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityFrames));
        }

        Channels = channels;
        _capacityFrames = capacityFrames;
        _ring = new float[checked(capacityFrames * channels)];
    }

    public int Channels { get; }
    public int CapacityFrames => _capacityFrames;

    /// <summary>Total frames ever committed. Monotonic; used to detect a stalled stream.</summary>
    public long TotalFrames => Interlocked.Read(ref _committedFrames);

    /// <summary>Producer: append <paramref name="byteLength"/> bytes of interleaved 32-bit float samples.</summary>
    public void Write(IntPtr buffer, int byteLength)
    {
        if (buffer == IntPtr.Zero || byteLength <= 0)
        {
            return;
        }

        var samples = byteLength / sizeof(float);
        var frames = samples / Channels;
        if (frames <= 0)
        {
            return;
        }

        var sourceOffsetSamples = 0;
        if (frames > _capacityFrames)
        {
            // Only the tail can survive; skip what would be overwritten immediately.
            sourceOffsetSamples = (frames - _capacityFrames) * Channels;
            frames = _capacityFrames;
        }

        var committed = Interlocked.Read(ref _committedFrames); // producer is the only writer
        Interlocked.Exchange(ref _reservedFrames, committed + frames); // full fence: reservation visible before any sample is overwritten

        var ringFrame = (int)(committed % _capacityFrames);
        var firstFrames = Math.Min(frames, _capacityFrames - ringFrame);
        var src = IntPtr.Add(buffer, sourceOffsetSamples * sizeof(float));

        Marshal.Copy(src, _ring, ringFrame * Channels, firstFrames * Channels);
        if (frames > firstFrames)
        {
            Marshal.Copy(IntPtr.Add(src, firstFrames * Channels * sizeof(float)), _ring, 0, (frames - firstFrames) * Channels);
        }

        Interlocked.Exchange(ref _committedFrames, committed + frames); // full fence: samples visible before the new count
    }

    /// <summary>Producer (tests / managed sources): append interleaved samples from an array.</summary>
    public void Write(float[] interleaved, int frameCount)
    {
        var handle = GCHandle.Alloc(interleaved, GCHandleType.Pinned);
        try
        {
            Write(handle.AddrOfPinnedObject(), frameCount * Channels * sizeof(float));
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Consumer: copy the latest <paramref name="frames"/> frames into <paramref name="destination"/> (oldest first).
    /// If fewer frames have ever been written, the missing oldest part is zero-filled (silence).
    /// Frames with an absolute index below <paramref name="discardBeforeFrame"/> are also zero-filled: after a gap in
    /// the stream (e.g. loopback delivered nothing while playback was paused) audio from before the gap must not be
    /// analysed as if it were contiguous with the new audio.
    /// Returns false if a consistent snapshot could not be obtained (producer lapped the reader).
    /// </summary>
    public bool TryCopyLatest(float[] destination, int frames, out long endFrame, long discardBeforeFrame = 0)
    {
        if (frames < 1 || frames > _capacityFrames)
        {
            throw new ArgumentOutOfRangeException(nameof(frames), frames, "Must be between 1 and the capacity.");
        }

        if (destination == null || destination.Length < frames * Channels)
        {
            throw new ArgumentException("Destination too small.", nameof(destination));
        }

        for (var attempt = 0; attempt < MaxSnapshotAttempts; attempt++)
        {
            var end = Interlocked.Read(ref _committedFrames);
            var available = (int)Math.Min(end - Math.Min(end, Math.Max(0, discardBeforeFrame)), frames);
            var missing = frames - available;

            if (missing > 0)
            {
                Array.Clear(destination, 0, missing * Channels);
            }

            var startFrame = end - available;
            var ringFrame = (int)(startFrame % _capacityFrames);
            var firstFrames = Math.Min(available, _capacityFrames - ringFrame);

            Array.Copy(_ring, ringFrame * Channels, destination, missing * Channels, firstFrames * Channels);
            if (available > firstFrames)
            {
                Array.Copy(_ring, 0, destination, (missing + firstFrames) * Channels, (available - firstFrames) * Channels);
            }

            Interlocked.MemoryBarrier(); // sample reads complete before the validation read
            var reserved = Interlocked.Read(ref _reservedFrames);

            // Any frame the producer may have touched is < reserved; it overwrote our region iff it reached
            // startFrame + capacity.
            if (reserved - startFrame <= _capacityFrames)
            {
                endFrame = end;
                return true;
            }
        }

        endFrame = 0;
        return false;
    }
}
