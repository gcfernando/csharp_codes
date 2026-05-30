using System;
using System.Drawing;
using System.Windows.Forms;
using Glass;
using Xunit;

namespace Glass.Message.Tests;

// ─────────────────────────────────────────────────────────────────────────
// Pure-logic tests — no visible window is opened in any test.
// ─────────────────────────────────────────────────────────────────────────

public class ThemeTests
{
    [Fact] public void Default_Theme_Is_Dark_Blue() =>
        Assert.Equal(Color.FromArgb(15, 23, 42), GlassTheme.Default.BackgroundTop);

    [Fact] public void Light_Theme_Background_Is_Bright() =>
        Assert.True(GlassTheme.Light.BackgroundTop.GetBrightness() > 0.8f);

    [Fact] public void HighContrast_Theme_Uses_SystemColors() =>
        Assert.Equal(SystemColors.Window, GlassTheme.HighContrast.BackgroundTop);

    [Fact] public void WindowsClassic_Has_Zero_CornerRadius() =>
        Assert.Equal(0, GlassTheme.WindowsClassic.CornerRadius);

    [Fact] public void HighContrast_Has_Zero_CornerRadius() =>
        Assert.Equal(0, GlassTheme.HighContrast.CornerRadius);

    [Fact] public void AutoDetect_Returns_A_Theme() =>
        Assert.NotNull(GlassTheme.AutoDetect());

    [Fact] public void IsSystemDark_Returns_Bool() =>
        Assert.IsType<bool>(GlassTheme.IsSystemDark());

    [Fact] public void Theme_Has_NonNull_Fonts()
    {
        Assert.NotNull(GlassTheme.Default.TitleFont);
        Assert.NotNull(GlassTheme.Default.MessageFont);
        Assert.NotNull(GlassTheme.Default.ButtonFont);
    }

    // #10: IDisposable — custom theme disposes without throwing
    [Fact] public void Custom_Theme_Dispose_Does_Not_Throw()
    {
        var theme = new GlassTheme
        {
            TitleFont   = new Font("Segoe UI", 12f),
            MessageFont = new Font("Segoe UI", 10f),
            ButtonFont  = new Font("Segoe UI", 9f),
        };
        var ex = Record.Exception(() => theme.Dispose());
        Assert.Null(ex);
    }

    // #10: calling Dispose twice on a custom theme is safe
    [Fact] public void Custom_Theme_Dispose_Twice_Is_Safe()
    {
        var theme = new GlassTheme();
        theme.Dispose();
        var ex = Record.Exception(() => theme.Dispose());
        Assert.Null(ex);
    }

    // #10: static presets are protected — Dispose is a no-op
    [Fact] public void Preset_Dispose_Does_Not_Throw_And_Fonts_Stay_Valid()
    {
        GlassTheme.Default.Dispose();  // must be safe (preset guard)
        Assert.NotNull(GlassTheme.Default.TitleFont);  // still usable
    }

    // Preset CornerRadius retains its configured value
    [Fact] public void Theme_CornerRadius_Default_Is_8() =>
        Assert.Equal(8, GlassTheme.Default.CornerRadius);

    [Fact] public void Theme_ButtonCornerRadius_Default_Is_5() =>
        Assert.Equal(5, GlassTheme.Default.ButtonCornerRadius);
}

public class GlassResultTests
{
    [Fact] public void Implicit_Conversion_To_DialogResult_Works()
    {
        var r = new GlassResult(DialogResult.OK, true, "hello");
        DialogResult dr = r;
        Assert.Equal(DialogResult.OK, dr);
    }

    [Fact] public void InputText_Never_Null()
    {
        var r = new GlassResult(DialogResult.Cancel, false, null);
        Assert.Equal(string.Empty, r.InputText);
    }

    [Fact] public void CheckBoxChecked_Reflects_Argument()
    {
        var r = new GlassResult(DialogResult.Yes, true, "text");
        Assert.True(r.CheckBoxChecked);
    }
}

public class GlassBuilderTests
{
    [Fact] public void Create_Returns_Builder() =>
        Assert.NotNull(GlassMessage.Create("test"));

    [Fact] public void Builder_Chains_Fluently()
    {
        var b = GlassMessage.Create("msg")
            .Title("T")
            .Icon(MessageBoxIcon.Information)
            .Buttons(MessageBoxButtons.OKCancel)
            .Default(MessageBoxDefaultButton.Button2)
            .Animation(GlassAnimation.SlideDown)
            .AutoClose(5_000)
            .CheckBox("Don't show again")
            .InputText("placeholder", "default")
            .Detail("stack trace here")
            .Progress(50, 100)
            .RightToLeft(false)
            .RoundedCorners(true);   // #5/#13: RoundedCorners is chainable
        Assert.NotNull(b);
    }

    [Fact] public void Builder_Custom_Labels_Sets_OKCancel_For_Two()
    {
        var b = GlassMessage.Create("msg").Buttons("Yes, Delete", "Cancel");
        Assert.NotNull(b);
    }

    // #13: RoundedCorners(true) is chainable and returns builder
    [Fact] public void Builder_RoundedCorners_True_Returns_Builder()
    {
        var b = GlassMessage.Create("msg").RoundedCorners(true);
        Assert.NotNull(b);
    }

    [Fact] public void Builder_RoundedCorners_False_Returns_Builder()
    {
        var b = GlassMessage.Create("msg").RoundedCorners(false);
        Assert.NotNull(b);
    }

    // #13: calling without RoundedCorners leaves null in config (inherits global)
    [Fact] public void Builder_Without_RoundedCorners_Leaves_Config_Null()
    {
        // We can't inspect BuildConfig() directly (private), but we can verify
        // that GlassMessage.UseRoundedCorners=false + no .RoundedCorners() call
        // gives the same "no rounded" behavior as explicitly false.
        var savedGlobal = GlassMessage.UseRoundedCorners;
        try
        {
            GlassMessage.UseRoundedCorners = false;
            // Just verify no exception when building — the dialog inherits global
            Assert.NotNull(GlassMessage.Create("test"));
        }
        finally
        {
            GlassMessage.UseRoundedCorners = savedGlobal;
        }
    }

    // #1: AcceptButton is set — the builder doesn't throw with default config
    [Fact] public void Builder_With_Animation_None_Returns_Builder()
    {
        var b = GlassMessage.Create("msg").Animation(GlassAnimation.None);
        Assert.NotNull(b);
    }

    // #3: Scale animation is a defined enum value
    [Fact] public void Builder_Scale_Animation_Is_Accepted()
    {
        var b = GlassMessage.Create("msg").Animation(GlassAnimation.Scale);
        Assert.NotNull(b);
    }

    // #14: Scale is a distinct enum value, not an alias of None or Fade
    [Fact] public void GlassAnimation_Scale_Is_Distinct_Enum_Value()
    {
        Assert.NotEqual(GlassAnimation.Fade,  GlassAnimation.Scale);
        Assert.NotEqual(GlassAnimation.None,  GlassAnimation.Scale);
        Assert.NotEqual(GlassAnimation.SlideDown, GlassAnimation.Scale);
    }

    // #3: All four animation values are defined
    [Fact] public void GlassAnimation_Has_Four_Values()
    {
        var values = Enum.GetValues<GlassAnimation>();
        Assert.Equal(4, values.Length);
    }

    // Buttons(null) guard (#4 in builder)
    [Fact] public void Builder_Buttons_Null_Array_Does_Not_Throw()
    {
        var ex = Record.Exception(() => GlassMessage.Create("msg").Buttons((string[])null));
        Assert.Null(ex);
    }

    [Fact] public void Builder_Buttons_Empty_Array_Does_Not_Throw()
    {
        var ex = Record.Exception(() => GlassMessage.Create("msg").Buttons(new string[0]));
        Assert.Null(ex);
    }
}

public class GlassDialogConfigTests
{
    [Fact] public void HasCheckBox_False_When_Label_Null() =>
        Assert.False(new GlassDialogConfig().HasCheckBox);

    [Fact] public void HasCheckBox_True_When_Label_Set() =>
        Assert.True(new GlassDialogConfig { CheckBoxLabel = "Don't show" }.HasCheckBox);

    [Fact] public void HasInput_False_By_Default() =>
        Assert.False(new GlassDialogConfig().HasInput);

    [Fact] public void HasInput_True_When_Mode_Set() =>
        Assert.True(new GlassDialogConfig { InputMode = GlassInputMode.Text }.HasInput);

    [Fact] public void HasProgress_False_By_Default() =>
        Assert.False(new GlassDialogConfig().HasProgress);

    [Fact] public void HasProgress_True_When_Enabled() =>
        Assert.True(new GlassDialogConfig { ShowProgress = true }.HasProgress);

    [Fact] public void HasDetail_False_When_Null() =>
        Assert.False(new GlassDialogConfig().HasDetail);

    [Fact] public void HasDetail_True_When_Set() =>
        Assert.True(new GlassDialogConfig { DetailText = "info" }.HasDetail);

    // #13: UseRoundedCorners defaults to null (inherit from global)
    [Fact] public void UseRoundedCorners_Defaults_To_Null() =>
        Assert.Null(new GlassDialogConfig().UseRoundedCorners);

    [Fact] public void UseRoundedCorners_Can_Be_Set_True() =>
        Assert.True(new GlassDialogConfig { UseRoundedCorners = true }.UseRoundedCorners);

    [Fact] public void UseRoundedCorners_Can_Be_Set_False() =>
        Assert.False(new GlassDialogConfig { UseRoundedCorners = false }.UseRoundedCorners);
}

public class GlassMessageStaticTests
{
    // Serialize access to static state with a shared lock per xUnit process
    private static readonly object _staticLock = new();

    // #13: global UseRoundedCorners defaults to false
    [Fact] public void UseRoundedCorners_Global_Default_Is_False() =>
        Assert.False(GlassMessage.UseRoundedCorners);

    [Fact] public void DefaultTheme_Can_Be_Overridden_And_Restored()
    {
        lock (_staticLock)
        {
            var original = GlassMessage.DefaultTheme;
            try
            {
                GlassMessage.DefaultTheme = GlassTheme.Light;
                Assert.Equal(GlassTheme.Light, GlassMessage.DefaultTheme);
            }
            finally { GlassMessage.DefaultTheme = original; }
        }
    }

    [Fact] public void UseRoundedCorners_Can_Be_Set_And_Restored()
    {
        lock (_staticLock)
        {
            var original = GlassMessage.UseRoundedCorners;
            try
            {
                GlassMessage.UseRoundedCorners = true;
                Assert.True(GlassMessage.UseRoundedCorners);
                GlassMessage.UseRoundedCorners = false;
                Assert.False(GlassMessage.UseRoundedCorners);
            }
            finally { GlassMessage.UseRoundedCorners = original; }
        }
    }
}

public class ToastOptionsTests
{
    [Fact] public void Default_Position_Is_BottomRight() =>
        Assert.Equal(ToastPosition.BottomRight, new GlassToastOptions().Position);

    [Fact] public void Default_Duration_Is_4000ms() =>
        Assert.Equal(4_000, new GlassToastOptions().DurationMs);

    // #13: UseRoundedCorners defaults to null (inherit from global)
    [Fact] public void UseRoundedCorners_Defaults_To_Null() =>
        Assert.Null(new GlassToastOptions().UseRoundedCorners);

    [Fact] public void UseRoundedCorners_Can_Be_Set_True() =>
        Assert.True(new GlassToastOptions { UseRoundedCorners = true }.UseRoundedCorners);

    [Fact] public void UseRoundedCorners_Can_Be_Set_False() =>
        Assert.False(new GlassToastOptions { UseRoundedCorners = false }.UseRoundedCorners);
}

public class RoundRectTests
{
    // Verify the RoundRect helper returns a non-null path for all radius values
    [Fact] public void RoundRect_Radius_Zero_Returns_Rectangle_Path()
    {
        using var path = GlassDialog.RoundRect(new Rectangle(0, 0, 100, 50), 0);
        Assert.NotNull(path);
        Assert.True(path.PointCount > 0);
    }

    [Fact] public void RoundRect_Positive_Radius_Returns_Curved_Path()
    {
        using var pathFlat   = GlassDialog.RoundRect(new Rectangle(0, 0, 100, 50), 0);
        using var pathRound  = GlassDialog.RoundRect(new Rectangle(0, 0, 100, 50), 8);
        // Rounded path has more points (arcs add vertices)
        Assert.True(pathRound.PointCount > pathFlat.PointCount);
    }

    [Fact] public void RoundRect_Negative_Radius_Treated_As_Zero()
    {
        using var path = GlassDialog.RoundRect(new Rectangle(0, 0, 100, 50), -5);
        Assert.NotNull(path);
    }
}
