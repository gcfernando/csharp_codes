using Xunit;

namespace Spectrum.Tests;

/// <summary>Analysis-timer decisions: silence, stalls, BASS errors, gaps and stream changes.</summary>
public class CaptureGateTests
{
    private const int Required = 4;
    private static readonly object StreamA = new();
    private static readonly object StreamB = new();

    [Fact]
    public void FlowingAudio_IsAnalysedAndFeedsTheHangDetector()
    {
        var gate = new CaptureGate(Required);
        for (var t = 1; t <= 20; t++)
        {
            var d = gate.Next(5000, t * 1500, StreamA, 0);
            Assert.Equal(CaptureAction.Analyze, d.Action);
            Assert.False(d.RecoverDevice);
        }

        Assert.Equal(0, gate.GapEndFrame);
    }

    [Fact]
    public void BassError_NeverReachesTheHangDetector_AndSettlesToSilence()
    {
        // Regression: a constant −1 level (device not started) used to look like a stuck level and reset the device.
        var gate = new CaptureGate(Required);
        for (var t = 1; t <= 50; t++)
        {
            var d = gate.Next(-1, 0, StreamA, 0);
            Assert.False(d.RecoverDevice);
            Assert.Equal(t >= Required ? CaptureAction.PublishSilence : CaptureAction.Wait, d.Action);
        }
    }

    [Fact]
    public void SingleTickWithoutNewFrames_IsJitterNotAGap()
    {
        var gate = new CaptureGate(Required);
        gate.Next(5000, 1500, StreamA, 0);
        var d = gate.Next(5000, 1500, StreamA, 0); // callback did not run between these two ticks
        Assert.Equal(CaptureAction.Analyze, d.Action);
        Assert.Equal(CaptureAction.Analyze, gate.Next(5000, 3000, StreamA, 0).Action);
        Assert.Equal(0, gate.GapEndFrame);
    }

    [Fact]
    public void ConfirmedStall_PublishesSilenceAndMarksTheGap()
    {
        // Regression: after a pause, pre-pause audio was analysed as if contiguous with the resumed audio.
        var gate = new CaptureGate(Required);
        gate.Next(5000, 9000, StreamA, 0);
        CaptureDecision d = default;
        for (var t = 0; t < Required; t++)
        {
            d = gate.Next(5000, 9000, StreamA, 0); // loopback delivers nothing while paused
        }

        Assert.Equal(CaptureAction.PublishSilence, d.Action);
        Assert.Equal(9000, gate.GapEndFrame);

        // Playback resumes: analysed again, and the gap stays in force for the history before it.
        Assert.Equal(CaptureAction.Analyze, gate.Next(5000, 10500, StreamA, 0).Action);
        Assert.Equal(9000, gate.GapEndFrame);
    }

    [Fact]
    public void StallDuringBassErrors_StillMarksTheGap()
    {
        var gate = new CaptureGate(Required);
        gate.Next(5000, 9000, StreamA, 0);
        for (var t = 0; t < Required; t++)
        {
            Assert.False(gate.Next(-1, 9000, StreamA, 0).RecoverDevice);
        }

        Assert.Equal(CaptureAction.Analyze, gate.Next(5000, 10500, StreamA, 0).Action);
        Assert.Equal(9000, gate.GapEndFrame);
    }

    [Fact]
    public void QuietTicksFollowedByOneLateCallback_AreNotAGap()
    {
        // Zero level with frames flowing, then a single tick where the callback was late: not a gap.
        var gate = new CaptureGate(Required);
        for (var t = 1; t < Required; t++)
        {
            gate.Next(0, t * 1500, StreamA, 0);
        }

        gate.Next(0, (Required - 1) * 1500, StreamA, 0);
        Assert.Equal(0, gate.GapEndFrame);
    }

    [Fact]
    public void ZeroLevelWithFramesFlowing_IsSilenceWithoutAGap()
    {
        // Digital silence is still delivered; the history is continuous, so there is nothing to exclude.
        var gate = new CaptureGate(Required);
        CaptureDecision d = default;
        for (var t = 1; t <= Required; t++)
        {
            d = gate.Next(0, t * 1500, StreamA, 0);
        }

        Assert.Equal(CaptureAction.PublishSilence, d.Action);
        Assert.False(d.RecoverDevice);
        Assert.Equal(0, gate.GapEndFrame);
    }

    [Fact]
    public void NewStream_ExcludesAudioFromThePreviousStream_AndIsNotTreatedAsStalled()
    {
        var gate = new CaptureGate(Required);
        gate.Next(5000, 20000, StreamA, 0);

        // Device recovered, same history reused: capture restarted at frame 20000.
        var d = gate.Next(5000, 20000, StreamB, 20000);
        Assert.Equal(CaptureAction.Analyze, d.Action);
        Assert.Equal(20000, gate.GapEndFrame);
    }

    private const int HangThreshold = 8;

    [Fact]
    public void SteadySignal_WithConstantLevel_NeverTriggersRecovery()
    {
        // Regression (live defect D1): a steady tone has a bit-identical peak level every tick. With frames flowing
        // that is a healthy stream, not a hang; the old rule reset the device every ~0.3 s.
        var gate = new CaptureGate(Required, HangThreshold);
        for (var t = 1; t <= 500; t++)
        {
            var d = gate.Next(214895823, t * 1520L, StreamA, 0);
            Assert.False(d.RecoverDevice, $"tick {t}");
            Assert.Equal(CaptureAction.Analyze, d.Action);
        }
    }

    [Fact]
    public void FrozenLevelWithNoFrames_TriggersOneRecoveryPerStall()
    {
        var gate = new CaptureGate(Required, HangThreshold);
        gate.Next(5000, 1520, StreamA, 0);
        var recoveries = 0;
        for (var t = 0; t < 100; t++)
        {
            if (gate.Next(5000, 1520, StreamA, 0).RecoverDevice)
            {
                recoveries++;
                Assert.True(t >= HangThreshold, $"recovery too early at stalled tick {t + 1}");
            }
        }

        Assert.Equal(1, recoveries); // a stale level during a long pause cannot cause a recovery loop

        // Recovery produced a new stream that is also stalled: still no second recovery until frames flow.
        for (var t = 0; t < 50; t++)
        {
            Assert.False(gate.Next(5000, 1520, StreamB, 1520).RecoverDevice);
        }

        // Frames flow again, then a new stall with a frozen level: recovery is re-armed.
        gate.Next(5000, 3040, StreamB, 1520);
        var rearmed = false;
        for (var t = 0; t < 20; t++)
        {
            rearmed |= gate.Next(5000, 3040, StreamB, 1520).RecoverDevice;
        }

        Assert.True(rearmed);
    }

    [Fact]
    public void StallWithZeroOrChangingLevel_IsNotAHang()
    {
        var gate = new CaptureGate(Required, HangThreshold);
        gate.Next(0, 1520, StreamA, 0);
        for (var t = 0; t < 50; t++)
        {
            Assert.False(gate.Next(0, 1520, StreamA, 0).RecoverDevice); // nothing playing: silence, not a hang
        }

        for (var t = 0; t < 50; t++)
        {
            Assert.False(gate.Next(1000 + t, 1520, StreamA, 0).RecoverDevice); // level still moving
        }
    }

    [Fact]
    public void SoundAfterSilence_ResetsTheQuietCount()
    {
        var gate = new CaptureGate(Required);
        for (var t = 1; t < Required; t++)
        {
            gate.Next(0, t * 1500, StreamA, 0);
        }

        Assert.Equal(CaptureAction.Analyze, gate.Next(5000, 9000, StreamA, 0).Action);
        Assert.Equal(CaptureAction.Analyze, gate.Next(0, 10500, StreamA, 0).Action); // count restarted
    }
}
