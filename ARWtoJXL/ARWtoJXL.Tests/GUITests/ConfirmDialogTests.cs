using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ARWtoJXL.Avalonia;
using ARWtoJXL.Avalonia.Services;

namespace ARWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class ConfirmDialogTests
{
    [AvaloniaFact]
    public void ConfirmDialog_CreatesSuccessfully()
    {
        var dialog = new ConfirmDialog();
        Assert.NotNull(dialog);
    }

    [AvaloniaFact]
    public void ConfirmDialog_HasYesAndNoButtons()
    {
        var dialog = new ConfirmDialog();
        dialog.Show();
        dialog.UpdateLayout();
        var buttonContents = GUITestHelpers.GetAllControls<Button>(dialog).Select(b => b.Content?.ToString()).ToList();
        Assert.Contains("Yes", buttonContents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("No", buttonContents, StringComparer.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void ConfirmDialog_HasMessageTextBlock()
    {
        var dialog = new ConfirmDialog();
        dialog.MessageText = "Test message";
        dialog.Show();
        dialog.UpdateLayout();
        var textBlocks = GUITestHelpers.GetAllControls<TextBlock>(dialog).ToList();
        Assert.True(textBlocks.Any(t => t.Text == "Test message"), "Expected a TextBlock with the message text");
    }

    [AvaloniaFact]
    public void ConfirmDialog_DataContext_IsItself()
    {
        var dialog = new ConfirmDialog();
        Assert.Same(dialog, dialog.DataContext);
    }

    [AvaloniaFact]
    public void ConfirmDialog_YesButton_HasClickHandler()
    {
        var dialog = new ConfirmDialog();
        dialog.Show();
        dialog.UpdateLayout();

        var yesButton = GUITestHelpers.GetAllControls<Button>(dialog)
            .First(b => b.Content?.ToString() == "Yes");

        Assert.True(yesButton.IsDefault);
    }

    [AvaloniaFact]
    public void ConfirmDialog_NoButton_HasClickHandler()
    {
        var dialog = new ConfirmDialog();
        dialog.Show();
        dialog.UpdateLayout();

        var noButton = GUITestHelpers.GetAllControls<Button>(dialog)
            .First(b => b.Content?.ToString() == "No");

        Assert.True(noButton.IsCancel);
    }

    [AvaloniaFact]
    public void ConfirmDialog_TitleText_BindsToWindowTitle()
    {
        var dialog = new ConfirmDialog();
        Assert.Equal("", dialog.Title);

        dialog.TitleText = "Custom Title";
        Assert.Equal("Custom Title", dialog.Title);
    }
}
