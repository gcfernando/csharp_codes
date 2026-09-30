namespace Spectrum;

internal enum CaptureAction
{
    /// <summary>Analyse the latest samples and publish a frame.</summary>
    Analyze,

    /// <summary>Confirmed silence or gap: publish the floor.</summary>
    PublishSilence,

    /// <summary>Not enough evidence either way (e.g. a transient BASS error): publish nothing.</summary>
    Wait,
}

internal readonly struct CaptureDecision
{
    public CaptureDecision(CaptureAction action, bool recoverDevice)
    {
        Action = action;
        RecoverDevice = recoverDevice;
    }

    public CaptureAction Action { get; }

    /// <summary>The capture looks hung: re-initialise the device.</summary>
    public bool RecoverDevice { get; }
}

/// <summary>
/// Per-tick decision for the analysis timer, kept free of BASS so it can be tested:
/// <list type="bullet">
/// <item>level &lt; 0 is a BASS error ("no data"): counts toward silence, never feeds the hang detector;</item>
/// <item>level == 0, or no new captured frames since the previous tick (loopback delivers nothing while
///   nothing plays), counts toward silence; after <c>silenceTicksRequired</c> consecutive ticks the floor is
///   published;</item>
/// <item>a confirmed stall marks a gap: frames captured before it must not be analysed together with the audio
///   that arrives after it (<see cref="GapEndFrame"/>). A single tick without new frames is normal jitter and
///   does not mark a gap.</item>
/// <item>device hang: no new frames while BASS keeps reporting the same non-zero level for more than
///   <c>hangTicksThreshold</c> ticks. A steady signal (test tone, drone, pad) legitimately has a constant level, so
///   level equality alone is never a hang — frames must also have stopped. At most one recovery is requested per
///   stall episode; the next needs frames to have flowed again, so a stale level during a pause cannot cause
///   a recovery loop.</item>
/// </list>
/// Timer-thread only.
/// </summary>
internal sealed class CaptureGate
{
    private readonly int _silenceTicksRequired;
    private readonly int _hangTicksThreshold;
    private object _stream;
    private long _lastTotalFrames;
    private int _quietTicks;
    private int _stallTicks;
    private int _lastLevel;
    private int _hangTicks;
    private bool _recoveryRequestedThisStall;

    public CaptureGate(int silenceTicksRequired, int hangTicksThreshold = int.MaxValue)
    {
        _silenceTicksRequired = silenceTicksRequired;
        _hangTicksThreshold = hangTicksThreshold;
    }

    /// <summary>Absolute frame index before which captured audio is excluded from analysis (0 = none).</summary>
    public long GapEndFrame { get; private set; }

    /// <param name="level">BASS_WASAPI_GetLevel result (−1 on error).</param>
    /// <param name="totalFrames">Frames captured so far in <paramref name="stream"/>.</param>
    /// <param name="stream">Identity of the current capture stream; a new identity resets stall tracking.</param>
    /// <param name="streamStartFrame">History frame count when that stream started: anything older belongs to a
    /// previous stream (e.g. before a device recovery) and is excluded from analysis.</param>
    public CaptureDecision Next(int level, long totalFrames, object stream, long streamStartFrame)
    {
        var sameStream = ReferenceEquals(stream, _stream);
        var stalled = sameStream && totalFrames == _lastTotalFrames;
        if (!sameStream)
        {
            GapEndFrame = streamStartFrame;
        }

        _stream = stream;
        _lastTotalFrames = totalFrames;

        var recover = UpdateHangDetection(level, stalled, sameStream);

        // Gap tracking is independent of the level (BASS can report an error level while the stream is stalled):
        // only consecutive ticks without new frames count, so a single late callback is not a gap.
        _stallTicks = stalled ? _stallTicks + 1 : 0;
        if (_stallTicks >= _silenceTicksRequired)
        {
            GapEndFrame = totalFrames;
        }

        if (level < 0)
        {
            _quietTicks++;
            return new CaptureDecision(_quietTicks >= _silenceTicksRequired ? CaptureAction.PublishSilence : CaptureAction.Wait, recover);
        }

        if (level == 0 || stalled)
        {
            _quietTicks++;
            if (_quietTicks >= _silenceTicksRequired)
            {
                return new CaptureDecision(CaptureAction.PublishSilence, recover);
            }
        }
        else
        {
            _quietTicks = 0;
        }

        return new CaptureDecision(CaptureAction.Analyze, recover);
    }

    private bool UpdateHangDetection(int level, bool stalled, bool sameStream)
    {
        // Frames flowing on the same stream ends the stall episode and re-arms recovery.
        if (sameStream && !stalled)
        {
            _recoveryRequestedThisStall = false;
        }

        _hangTicks = stalled && level > 0 && level == _lastLevel ? _hangTicks + 1 : 0;
        _lastLevel = level;

        if (_hangTicks > _hangTicksThreshold && !_recoveryRequestedThisStall)
        {
            _hangTicks = 0;
            _quietTicks = 0;
            _recoveryRequestedThisStall = true;
            return true;
        }

        return false;
    }
}
