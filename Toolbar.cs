using Avalonia.Controls;

public class Toolbar : DockPanel
{
    private readonly MenuItem _micItem;
    private readonly MenuItem _gameItem;
    private readonly MenuItem _discordItem;
    private readonly MenuItem _musicItem;
    private readonly MenuItem _desktopItem;
    private readonly MenuItem _masterItem;
    private readonly MenuItem _removeEmptyItem;
    private readonly MenuItem _frameBlendOnlyItem;
    private readonly MenuItem _saveSettingsItem;

    public Toolbar()
    {
        _micItem = CreateCheckableItem("Combine Mic Audio");
        _gameItem = CreateCheckableItem("Combine Game Audio");
        _discordItem = CreateCheckableItem("Combine Discord Audio");
        _musicItem = CreateCheckableItem("Combine Music Audio");
        _desktopItem = CreateCheckableItem("Combine Desktop Audio");
        _masterItem = CreateCheckableItem("Use Master Track");
        _removeEmptyItem = CreateCheckableItem("Remove Empty Audio Channels");
        _frameBlendOnlyItem = CreateCheckableItem("Only Frameblend Clips");
        _saveSettingsItem = CreateCheckableItem("Save");

        WireMasterExclusivity();

        var optionsMenu = new Menu
        {
            ItemsSource = new[]
            {
                CreateLoggerMenuItem(), CreateOptionsMenuItem(), CreateSettingsMenuItem()
            }
        };

        Children.Add(optionsMenu);
    }

    public bool CombineMicAudio => _micItem.IsChecked;
    public bool CombineGameAudio => _gameItem.IsChecked;
    public bool CombineDiscordAudio => _discordItem.IsChecked;
    public bool CombineMusicAudio => _musicItem.IsChecked;
    public bool CombineDesktopAudio => _desktopItem.IsChecked;
    public bool UseMaster => _masterItem.IsChecked;
    public bool RemoveEmptyAudioChannels => _removeEmptyItem.IsChecked;
    public bool FrameBlendOnly => _frameBlendOnlyItem.IsChecked;

    private void WireMasterExclusivity()
    {
        var combineItems = new[] { _micItem, _gameItem, _discordItem, _musicItem, _desktopItem };

        _masterItem.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuItem.IsCheckedProperty && _masterItem.IsChecked)
            {
                foreach (var item in combineItems)
                {
                    item.IsChecked = false;
                }
            }
        };

        foreach (var item in combineItems)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.Property == MenuItem.IsCheckedProperty && item.IsChecked)
                {
                    _masterItem.IsChecked = false;
                }
            };
        }
    }

    private static MenuItem CreateCheckableItem(string header) => new()
    {
        Header = header,
        ToggleType = MenuItemToggleType.CheckBox,
        StaysOpenOnClick = true
    };

    private MenuItem CreateOptionsMenuItem()
    {
        var optionsMenuItem = new MenuItem
        {
            Header = "_Options",
            ItemsSource = new Control[]
            {
                _desktopItem,
                _gameItem,
                _micItem,
                _musicItem,
                _discordItem,
                _masterItem,
                new Separator(),
                _removeEmptyItem,
                _frameBlendOnlyItem
            }
        };


        return optionsMenuItem;
    }

    private MenuItem CreateSettingsMenuItem()
    {
        var settingsMenuItem = new MenuItem
        {
            Header = "_Settings"
        };


        return settingsMenuItem;
    }

    private MenuItem CreateLoggerMenuItem()
    {
        var loggerMenuItem = new MenuItem
        {
            Header = "_Logger"
        };

        loggerMenuItem.Click += (_, _) => OpenLoggerClicked?.Invoke(this, EventArgs.Empty);


        return loggerMenuItem;
    }

    public AppSettings CaptureSettings => new(
    CombineGameAudio, CombineMicAudio, CombineDiscordAudio, CombineMusicAudio, CombineDesktopAudio, UseMaster, RemoveEmptyAudioChannels
    );

    public void ApplySettings(AppSettings s)
    {
        _gameItem.IsChecked = s.CombineGameAudio;
        _micItem.IsChecked = s.CombineMicAudio;
        _discordItem.IsChecked = s.CombineDiscordAudio;
        _desktopItem.IsChecked = s.CombineDesktopAudio;
        _musicItem.IsChecked = s.CombineMusicAudio;
        _removeEmptyItem.IsChecked = s.RemoveEmptyAudioChannels;
        _masterItem.IsChecked = s.UseMaster;
    }

    public event EventHandler? OpenLoggerClicked;
    public event EventHandler? SaveSettingsClicked;
}
