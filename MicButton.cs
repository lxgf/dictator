using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace Dictator;

public enum MicButtonState { Idle, Recording, Processing, Error }

// Code-only control (no XAML) — avoids XamlCompiler issues with custom controls.
public sealed class MicButton : Grid
{
    private MicButtonState _state = MicButtonState.Idle;
    private readonly DispatcherTimer _pulseTimer;
    private double _pulseScale = 1.0;
    private bool _pulseGrowing = true;

    private readonly Ellipse _pulseRing;
    private readonly Ellipse _mainCircle;
    private readonly FontIcon _micIcon;
    private readonly ProgressRing _spinner;
    private readonly ScaleTransform _pulseTransform;

    public MicButton()
    {
        Width = 104;
        Height = 104;

        try { ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand); }
        catch { }

        _pulseTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };

        _pulseRing = new Ellipse
        {
            Width = 90,
            Height = 90,
            Fill = MakeBrush(64, 220, 60, 60),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = _pulseTransform
        };

        _mainCircle = new Ellipse
        {
            Width = 72,
            Height = 72,
            Fill = MakeBrush(255, 60, 60, 60),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _micIcon = new FontIcon
        {
            Glyph = "\uE720",
            FontSize = 28,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _spinner = new ProgressRing
        {
            Width = 38,
            Height = 38,
            IsActive = false,
            Foreground = new SolidColorBrush(Colors.White),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Children.Add(_pulseRing);
        Children.Add(_mainCircle);
        Children.Add(_micIcon);
        Children.Add(_spinner);

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _pulseTimer.Tick += OnPulseTick;
        _pulseTimer.Start();

        PointerEntered += (_, _) => { if (_state == MicButtonState.Idle) _mainCircle.Fill = MakeBrush(255, 80, 80, 80); };
        PointerExited  += (_, _) => { if (_state == MicButtonState.Idle) _mainCircle.Fill = MakeBrush(255, 60, 60, 60); };
    }

    private void OnPulseTick(object? sender, object e)
    {
        if (_state != MicButtonState.Recording) return;
        _pulseScale += _pulseGrowing ? 0.015 : -0.015;
        if (_pulseScale >= 1.15) _pulseGrowing = false;
        if (_pulseScale <= 0.95) _pulseGrowing = true;
        _pulseTransform.ScaleX = _pulseScale;
        _pulseTransform.ScaleY = _pulseScale;
    }

    public void SetState(MicButtonState state)
    {
        _state = state;
        _pulseScale = 1.0;
        _pulseGrowing = true;
        _pulseTransform.ScaleX = 1;
        _pulseTransform.ScaleY = 1;

        switch (state)
        {
            case MicButtonState.Idle:
                _mainCircle.Fill      = MakeBrush(255, 60, 60, 60);
                _pulseRing.Visibility = Visibility.Collapsed;
                _micIcon.Visibility   = Visibility.Visible;
                _spinner.IsActive     = false;
                _spinner.Visibility   = Visibility.Collapsed;
                break;

            case MicButtonState.Recording:
                _mainCircle.Fill      = MakeBrush(255, 220, 60, 60);
                _pulseRing.Visibility = Visibility.Visible;
                _micIcon.Visibility   = Visibility.Visible;
                _spinner.IsActive     = false;
                _spinner.Visibility   = Visibility.Collapsed;
                break;

            case MicButtonState.Processing:
                _mainCircle.Fill      = MakeBrush(255, 255, 160, 0);
                _pulseRing.Visibility = Visibility.Collapsed;
                _micIcon.Visibility   = Visibility.Collapsed;
                _spinner.IsActive     = true;
                _spinner.Visibility   = Visibility.Visible;
                break;

            case MicButtonState.Error:
                _mainCircle.Fill      = MakeBrush(255, 180, 40, 40);
                _pulseRing.Visibility = Visibility.Collapsed;
                _micIcon.Visibility   = Visibility.Visible;
                _spinner.IsActive     = false;
                _spinner.Visibility   = Visibility.Collapsed;
                break;
        }
    }

    private static SolidColorBrush MakeBrush(byte a, byte r, byte g, byte b) =>
        new(Color.FromArgb(a, r, g, b));
}
