using Avalonia.Controls;

public class LoggerWindow : Window
{

    public LoggerWindow()
    {
        Title = "Logger";
        Width = 400;
        Height = 350;
        Content = new TextBlock { Text = "Logger goes here" };
        IsVisible = false;

    }

}
