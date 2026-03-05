using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Dictator;

public class HistoryWindow : Window
{
    public HistoryWindow()
    {
        // Custom title bar like main window
        ExtendsContentIntoTitleBar = true;

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(true, false);
        }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(500, 600));

        var root = new Grid
        {
            RequestedTheme = ElementTheme.Dark,
            Background = Br(18, 18, 18)
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ─── Title bar ──────────────────────────────────────────────
        var titleBar = new Grid { Background = Br(25, 25, 25) };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsHitTestVisible = false
        };
        titleStack.Children.Add(new FontIcon
        {
            Glyph = "\uE81C",
            FontSize = 14,
            Foreground = Br(150, 150, 150),
            Margin = new Thickness(0, 0, 8, 0)
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "История",
            Foreground = Br(200, 200, 200),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(titleStack, 0);
        titleBar.Children.Add(titleStack);

        var closeBtn = new Button
        {
            Width = 46,
            Height = 44,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new FontIcon { Glyph = "\uE8BB", FontSize = 11, Foreground = Br(150, 150, 150) }
        };
        closeBtn.Click += (_, _) => Close();
        Grid.SetColumn(closeBtn, 1);
        titleBar.Children.Add(closeBtn);

        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);
        SetTitleBar(titleBar);

        // ─── Content ────────────────────────────────────────────────
        var entries = RecognitionHistory.GetAll();
        entries.Reverse();

        var contentPanel = new Grid { Padding = new Thickness(12, 8, 12, 12) };
        Grid.SetRow(contentPanel, 1);

        if (entries.Count == 0)
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = "История пуста",
                Foreground = Br(140, 140, 140),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            var list = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                Padding = new Thickness(0)
            };

            foreach (var entry in entries)
            {
                var item = new Grid
                {
                    Padding = new Thickness(12, 10, 8, 10),
                    Background = Br(35, 35, 35),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 0, 6),
                    ColumnSpacing = 8
                };
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                item.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = entry.Text,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Br(230, 230, 230),
                    FontSize = 13
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = entry.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    Foreground = Br(100, 100, 100),
                    FontSize = 10
                });
                Grid.SetColumn(textStack, 0);
                item.Children.Add(textStack);

                var copyBtn = new Button
                {
                    Width = 36,
                    Height = 36,
                    Padding = new Thickness(0),
                    Background = Br(50, 50, 50),
                    BorderThickness = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = new FontIcon { Glyph = "\uE8C8", FontSize = 14, Foreground = Br(204, 204, 204) }
                };
                ToolTipService.SetToolTip(copyBtn, "Копировать");
                var capturedText = entry.Text;
                copyBtn.Click += (_, _) =>
                {
                    var dp = new DataPackage();
                    dp.SetText(capturedText);
                    Clipboard.SetContent(dp);

                    copyBtn.Content = new FontIcon { Glyph = "\uE8FB", FontSize = 14, Foreground = new SolidColorBrush(Colors.LimeGreen) };
                    Task.Delay(1000).ContinueWith(_ =>
                        DispatcherQueue.TryEnqueue(() =>
                            copyBtn.Content = new FontIcon { Glyph = "\uE8C8", FontSize = 14, Foreground = Br(204, 204, 204) }));
                };
                Grid.SetColumn(copyBtn, 1);
                item.Children.Add(copyBtn);

                list.Items.Add(item);
            }

            contentPanel.Children.Add(list);
        }

        root.Children.Add(contentPanel);
        Content = root;
    }

    private static SolidColorBrush Br(byte r, byte g, byte b) =>
        new(Windows.UI.Color.FromArgb(255, r, g, b));
}
