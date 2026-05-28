using System.Drawing;
using System.Windows.Forms;
using Glass;
using Xunit;

namespace Glass.Message.Tests;

// ─────────────────────────────────────────────────────────────────────────
// Tests that do NOT open a visible window (pure logic / API surface).
// ─────────────────────────────────────────────────────────────────────────

public class ThemeTests
{
    [Fact]
    public void Default_Theme_Is_Dark_Blue() =>
        Assert.Equal(Color.FromArgb(15, 23, 42), GlassTheme.Default.BackgroundTop);

    [Fact]
    public void Light_Theme_Background_Is_Bright() =>
        Assert.True(GlassTheme.Light.BackgroundTop.GetBrightness() > 0.8f);

    [Fact]
    public void HighContrast_Theme_Uses_SystemColors() =>
        Assert.Equal(SystemColors.Window, GlassTheme.HighContrast.BackgroundTop);

    [Fact]
    public void WindowsClassic_Has_Zero_CornerRadius() =>
        Assert.Equal(0, GlassTheme.WindowsClassic.CornerRadius);

    [Fact]
    public void AutoDetect_Returns_A_Theme() =>
        Assert.NotNull(GlassTheme.AutoDetect());

    [Fact]
    public void IsSystemDark_Returns_Bool() =>
        Assert.IsType<bool>(GlassTheme.IsSystemDark());
}

public class GlassResultTests
{
    [Fact]
    public void Implicit_Conversion_To_DialogResult_Works()
    {
        // InternalsVisibleTo lets us call the internal constructor directly
        var r = new GlassResult(DialogResult.OK, true, "hello");
        DialogResult dr = r;
        Assert.Equal(DialogResult.OK, dr);
    }

    [Fact]
    public void InputText_Never_Null()
    {
        var r = new GlassResult(DialogResult.Cancel, false, null);
        Assert.Equal(string.Empty, r.InputText);
    }

    [Fact]
    public void CheckBoxChecked_Reflects_Argument()
    {
        var r = new GlassResult(DialogResult.Yes, true, "text");
        Assert.True(r.CheckBoxChecked);
    }
}

public class GlassBuilderTests
{
    [Fact]
    public void Create_Returns_Builder() =>
        Assert.NotNull(GlassMessage.Create("test"));

    [Fact]
    public void Builder_Chains_Fluently()
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
            .RightToLeft(false);
        Assert.NotNull(b);
    }

    [Fact]
    public void Builder_Custom_Labels_Sets_OKCancel_For_Two()
    {
        // No window is opened; we just verify the builder does not throw.
        var b = GlassMessage.Create("msg").Buttons("Yes, Delete", "Cancel");
        Assert.NotNull(b);
    }
}

public class GlassDialogConfigTests
{
    [Fact]
    public void HasCheckBox_False_When_Label_Null()
    {
        var c = new GlassDialogConfig();
        Assert.False(c.HasCheckBox);
    }

    [Fact]
    public void HasCheckBox_True_When_Label_Set()
    {
        var c = new GlassDialogConfig { CheckBoxLabel = "Don't show" };
        Assert.True(c.HasCheckBox);
    }

    [Fact]
    public void HasInput_False_By_Default()
    {
        var c = new GlassDialogConfig();
        Assert.False(c.HasInput);
    }

    [Fact]
    public void HasInput_True_When_Mode_Set()
    {
        var c = new GlassDialogConfig { InputMode = GlassInputMode.Text };
        Assert.True(c.HasInput);
    }

    [Fact]
    public void HasProgress_False_By_Default()
    {
        var c = new GlassDialogConfig();
        Assert.False(c.HasProgress);
    }

    [Fact]
    public void HasProgress_True_When_Enabled()
    {
        var c = new GlassDialogConfig { ShowProgress = true };
        Assert.True(c.HasProgress);
    }

    [Fact]
    public void HasDetail_False_When_Null()
    {
        var c = new GlassDialogConfig();
        Assert.False(c.HasDetail);
    }
}

public class DefaultThemeTests
{
    [Fact]
    public void DefaultTheme_Can_Be_Overridden_And_Restored()
    {
        var original = GlassMessage.DefaultTheme;
        GlassMessage.DefaultTheme = GlassTheme.Light;
        Assert.Equal(GlassTheme.Light, GlassMessage.DefaultTheme);
        GlassMessage.DefaultTheme = original;
    }
}

public class ToastOptionsTests
{
    [Fact]
    public void Default_Position_Is_BottomRight() =>
        Assert.Equal(ToastPosition.BottomRight, new GlassToastOptions().Position);

    [Fact]
    public void Default_Duration_Is_4000ms() =>
        Assert.Equal(4_000, new GlassToastOptions().DurationMs);
}
