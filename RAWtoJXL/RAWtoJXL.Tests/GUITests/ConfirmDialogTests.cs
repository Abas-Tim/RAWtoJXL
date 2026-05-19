using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using RAWtoJXL.Avalonia;
using RAWtoJXL.Avalonia.Services;

namespace RAWtoJXL.Tests.GUITests;

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
        Assert.IsType<ConfirmDialog.ConfirmDialogViewModel>(dialog.DataContext);
    }

    [AvaloniaFact]
    public void ConfirmDialog_YesButton_Click_ClosesDialog()
    {
        var dialog = new ConfirmDialog { MessageText = "Delete?" };
        dialog.Show();
        dialog.UpdateLayout();

        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        var yesButton = GUITestHelpers.GetAllControls<Button>(dialog)
            .First(b => b.Content?.ToString() == "Yes");

        yesButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(closed, "Dialog should be closed after clicking Yes");
    }

    [AvaloniaFact]
    public void ConfirmDialog_NoButton_Click_ClosesDialog()
    {
        var dialog = new ConfirmDialog { MessageText = "Delete?" };
        dialog.Show();
        dialog.UpdateLayout();

        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        var noButton = GUITestHelpers.GetAllControls<Button>(dialog)
            .First(b => b.Content?.ToString() == "No");

        noButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(closed, "Dialog should be closed after clicking No");
    }

    [AvaloniaFact]
    public void ConfirmDialog_YesButton_IsDefault()
    {
        var dialog = new ConfirmDialog();
        dialog.Show();
        dialog.UpdateLayout();

        var yesButton = GUITestHelpers.GetAllControls<Button>(dialog)
            .First(b => b.Content?.ToString() == "Yes");

        Assert.True(yesButton.IsDefault);
    }

    [AvaloniaFact]
    public void ConfirmDialog_NoButton_IsCancel()
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

    [AvaloniaFact]
    public void ConfirmDialog_MessageText_BindsToDataContext()
    {
        var dialog = new ConfirmDialog { MessageText = "Overwrite file?" };
        dialog.Show();
        dialog.UpdateLayout();

        var vm = (ConfirmDialog.ConfirmDialogViewModel)dialog.DataContext!;
        Assert.Equal("Overwrite file?", vm.MessageText);

        dialog.MessageText = "Changed message";
        Assert.Equal("Changed message", vm.MessageText);
    }
}
