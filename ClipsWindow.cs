using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Layout;

class ClipsWindow : Window
{
    private readonly TextBlock _statusMessageText = new TextBlock
    {
        Text = string.Empty,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.Red,
        FontSize = 18,
        IsVisible = false

    };
    private readonly Toolbar _toolbar = new();

    private CancellationTokenSource? _workCts;
    private Task _activeWork = Task.CompletedTask;


    private readonly CheckBox frameBlendOnlyCheckbox = new()
    {
        Content = "Only Frameblend clips"
    };

    public ClipsWindow()
    {
        Title = "Autoblend";
        Width = 350;
        Height = 400;
        SizeToContent = SizeToContent.Width;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        _toolbar.SaveSettingsClicked += OnSaveSettingsClicked;
        _toolbar.OpenLoggerClicked += OnOpenLoggerClicked;

        Content = BuildLayout();
    }

    private LoggerWindow? _loggerWindow;

    private void OnOpenLoggerClicked(object? sender, EventArgs e)
    {
        if (_loggerWindow is { IsVisible: true })
        {
            _loggerWindow.Activate();
            return;
        }
        _loggerWindow = new LoggerWindow();
        _loggerWindow.Show();
    }

    private void OnSaveSettingsClicked(object? sender, EventArgs e)
    {
        _toolbar.ApplySettings(_toolbar.CaptureSettings);
    }

    private Grid BuildLayout()
    {
        Grid.SetRow(_statusMessageText, 1);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Children = { _toolbar, _statusMessageText, CreateDropZone() },
        };
    }

    private Border CreateDropZone()
    {
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

        Grid.SetRow(dropZone, 2);
        return dropZone;

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

            var audioOptions = new AudioFixOptions(
            CombineMicAudio: _toolbar.CombineMicAudio,
            CombineGameAudio: _toolbar.CombineGameAudio,
            CombineDiscordAudio: _toolbar.CombineDiscordAudio,
            CombineMusicAudio: _toolbar.CombineMusicAudio,
            CombineDesktopAudio: _toolbar.CombineDesktopAudio,
            UseMaster: _toolbar.UseMaster,
            RemoveEmptyAudioChannels: _toolbar.RemoveEmptyAudioChannels
            );

            _workCts?.Dispose();
            _workCts = new CancellationTokenSource();
            var ct = _workCts.Token;

            try
            {
                var fixTask = handler.FixClipAudioChannels(audioOptions, ct);
                _activeWork = fixTask;
                var result = await fixTask;
                if (!result.IsSuccess)
                {
                    // handle error later lol

                    return;
                }
                var blendTask = handler.FrameBlendClips(ct);
                _activeWork = blendTask;
                result = await blendTask;
                if (!result.IsSuccess)
                {
                    // handler other error later lol
                    return;
                }

                _statusMessageText.Text = result.IsSuccess ? "Done!" : $"{result.FailCount} clip(s) failed: {string.Join(", ", result.Errors)}";
                _statusMessageText.IsVisible = true;
            }
            catch (OperationCanceledException)
            {
                _statusMessageText.Text = "Cancelled";
                _statusMessageText.IsVisible = true;
            }
            finally
            {
                e.Handled = true;
            }
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_workCts is { IsCancellationRequested: false })
        {
            e.Cancel = true;
            _statusMessageText.Text = "Cancelling";
            _statusMessageText.IsVisible = true;
            _workCts.Cancel();
            try
            {

                await _activeWork;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("hello");
            }
            catch (Exception)
            {

            }
            Close();
        }
        base.OnClosing(e);
    }
}
