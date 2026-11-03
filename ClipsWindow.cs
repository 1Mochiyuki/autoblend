using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Layout;

class ClipsWindow : Window
{
    private readonly TextBlock _statusMessageText = new TextBlock
    {
        Text = "",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.Red,
        FontSize = 18,
        IsVisible = false

    };
    public ClipsWindow()
    {
        Title = "Autoblend";
        Width = 400;
        Height = 350;
        SizeToContent = SizeToContent.Width;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        var dropZone = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1c1c1c")),
            BorderThickness = new Thickness(2),
            Margin = new Thickness(10),

            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,


            Child = new TextBlock
            {
                Text = "Drop files here",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.WhiteSmoke,
                FontSize = 18
            }
        };

        DragDrop.SetAllowDrop(dropZone, true);
        DragDrop.AddDragOverHandler(dropZone, OnDragOver);
        DragDrop.AddDropHandler(dropZone, OnDrop);

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { _statusMessageText, dropZone }
        };

        Grid.SetRow(dropZone, 1);
        Content = contentGrid;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {

        if (e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {

        if (e.DataTransfer.TryGetFiles() is { } files)
        {


            var handler = new ClipsHandler();
            var validClips = handler.ProcessUploadedClips(files);
            if (handler.UploadErrorMessage != null)
            {
                _statusMessageText.Text = handler.UploadErrorMessage;
                _statusMessageText.IsVisible = true;
                e.Handled = true;
            }
            var result = await handler.FixClipAudioChannels(new AudioFixOptions(IncludeDiscord: false, IncludeMusic: false, IncludeDesktop: false, UseMaster: false));
            _statusMessageText.Text = result.Success ? "Done!" : $"{result.FailCount} clip(s) failed: {string.Join(", ", result.Errors)}";
            _statusMessageText.IsVisible = true;

            e.Handled = true;
        }
    }
}
