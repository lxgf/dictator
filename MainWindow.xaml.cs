using H.NotifyIcon;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace Dictator;

public sealed partial class MainWindow : Window
{
    private readonly SpeechRecognizer _recognizer;
    private bool _isRecording = false;
    private GlobalHotkey? _hotkey;

    public MainWindow()
    {
        this.InitializeComponent();

        SetupWindow();
        SetupTrayMenu();

        RootGrid.Loaded += (_, _) =>
        {
            // Tray icon — try embedded resource first (SingleFile), then file on disk
            try
            {
                var stream = typeof(MainWindow).Assembly
                    .GetManifestResourceStream("Dictator.icon.ico");
                if (stream != null)
                {
                    TrayIcon.Icon = new System.Drawing.Icon(stream);
                }
                else
                {
                    var icoPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(Environment.ProcessPath)!,
                        "Assets", "icon.ico");
                    TrayIcon.Icon = new System.Drawing.Icon(icoPath);
                }
                TrayIcon.ForceCreate();
            }
            catch { }

            // Global hotkey (Ctrl+Shift+R)
            SetupGlobalHotkey();
        };

        _recognizer = new SpeechRecognizer();
        _recognizer.OnResult       += OnSpeechResult;
        _recognizer.OnError        += OnSpeechError;
        _recognizer.OnStateChanged += OnStateChanged;
    }

    // ─── Window setup ────────────────────────────────────────────────────────

    private void SetupWindow()
    {
        try { SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop(); }
        catch
        {
            try { SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop(); }
            catch { }
        }

        // Custom title bar: content fills the title bar area, our Grid handles drag
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);

        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            // border=true keeps native drag working, titlebar=false hides OS caption buttons
            presenter.SetBorderAndTitleBar(true, false);
        }

        AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 400));

        // OS close → minimize to tray
        AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            AppWindow.Hide();
        };
    }

    private void SetupTrayMenu()
    {
        var menu = new MenuFlyout();

        var openItem = new MenuFlyoutItem { Text = "Открыть" };
        openItem.Click += (_, _) => ShowMainWindow();

        var hk = AppSettings.GetHotkey();
        var recordItem = new MenuFlyoutItem { Text = $"Записать" };
        recordItem.Click += async (_, _) => { ShowMainWindow(); await ToggleRecording(); };

        var exitItem = new MenuFlyoutItem { Text = "Выход" };
        exitItem.Click += (_, _) =>
        {
            _hotkey?.Dispose();
            TrayIcon?.Dispose();
            Application.Current.Exit();
        };

        menu.Items.Add(openItem);
        menu.Items.Add(recordItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);

        TrayIcon.ContextFlyout = menu;
        TrayIcon.DoubleClickCommand = new RelayCommand(ShowMainWindow);
    }

    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public RelayCommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void ShowMainWindow()
    {
        AppWindow.Show();
        this.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hwnd);
    }

    // ─── Global hotkey ──────────────────────────────────────────────────────

    private void SetupGlobalHotkey()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _hotkey = new GlobalHotkey(hwnd);
        _hotkey.HotkeyPressed += () =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowMainWindow();
                _ = ToggleRecording();
            });
        };
        RegisterCurrentHotkey();
    }

    private void RegisterCurrentHotkey()
    {
        var hk = AppSettings.GetHotkey();
        _hotkey?.Register(hk.Modifiers, hk.Vk);
    }

    // ─── Mic button ───────────────────────────────────────────────────────────

    private void BtnMic_Tapped(object sender, TappedRoutedEventArgs e) =>
        _ = ToggleRecording();

    private async Task ToggleRecording()
    {
        if (!AppSettings.HasApiKey())
        {
            await ShowSettingsDialogAsync();
            return;
        }

        if (_isRecording)
        {
            _isRecording = false;
            BtnMic.SetState(MicButtonState.Processing);
            LblStatus.Text = "Распознаю...";
            await _recognizer.StopAndRecognize();
        }
        else
        {
            _isRecording = true;
            BtnMic.SetState(MicButtonState.Recording);
            LblStatus.Text = "Говорите...";
            LblHint.Text   = $"Нажмите снова или {AppSettings.GetHotkey().DisplayName} для остановки";
            TxtResult.Text = "";
            BtnCopy.Visibility = Visibility.Collapsed;
            _recognizer.StartRecording();
        }
    }

    // ─── Recognizer callbacks ─────────────────────────────────────────────────

    private void OnSpeechResult(string text)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isRecording = false;
            BtnMic.SetState(MicButtonState.Idle);

            if (string.IsNullOrWhiteSpace(text))
            {
                LblStatus.Text = "Ничего не распознано";
                LblHint.Text   = "Текст скопируется автоматически";
                return;
            }

            TxtResult.Text = text;
            BtnCopy.Visibility = Visibility.Visible;
            RecognitionHistory.Add(text);

            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);

            LblStatus.Text = "✓ Скопировано в буфер";
            LblHint.Text   = "Нажмите Ctrl+V чтобы вставить";

            try
            {
                TrayIcon?.ShowNotification(
                    "Диктатор",
                    text.Length > 60 ? text[..60] + "..." : text);
            }
            catch { }
        });
    }

    private void OnSpeechError(string error)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isRecording = false;
            BtnMic.SetState(MicButtonState.Error);
            LblStatus.Text = "Ошибка: " + error;
            LblHint.Text   = "Текст скопируется автоматически";

            Task.Delay(3000).ContinueWith(_ =>
                DispatcherQueue.TryEnqueue(() => BtnMic.SetState(MicButtonState.Idle)));
        });
    }

    private void OnStateChanged(string state) =>
        DispatcherQueue.TryEnqueue(() => LblStatus.Text = state);

    // ─── Copy button ──────────────────────────────────────────────────────────

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtResult.Text)) return;
        var dp = new DataPackage();
        dp.SetText(TxtResult.Text);
        Clipboard.SetContent(dp);
        BtnCopy.Content = new FontIcon { Glyph = "\uE8FB", FontSize = 13, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen) };
        Task.Delay(1000).ContinueWith(_ =>
            DispatcherQueue.TryEnqueue(() => BtnCopy.Content = new FontIcon { Glyph = "\uE8C8", FontSize = 13, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 204, 204, 204)) }));
    }

    // ─── History button ────────────────────────────────────────────────────────

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = new HistoryWindow();
        historyWindow.Activate();
    }

    // ─── Close button ─────────────────────────────────────────────────────────

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        AppWindow.Hide();
    }

    // ─── Settings ─────────────────────────────────────────────────────────────

    private void BtnSettings_Click(object sender, RoutedEventArgs e) =>
        _ = ShowSettingsDialogAsync();

    private async Task ShowSettingsDialogAsync()
    {
        var panel = new SettingsPanel();
        var dialog = new ContentDialog
        {
            Title             = "Настройки",
            Content           = panel,
            PrimaryButtonText = "Сохранить",
            CloseButtonText   = "Отмена",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = Content.XamlRoot,
            RequestedTheme    = ElementTheme.Dark
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            AppSettings.SetApiKey(panel.ApiKey);
            AppSettings.SetHotkey(panel.Hotkey);
            RegisterCurrentHotkey();
        }
    }
}
