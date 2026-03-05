using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Dictator;

// ─── Hotkey definition ──────────────────────────────────────────────────────

public record HotkeyDef(uint Modifiers, uint Vk)
{
    // Default: Ctrl+Shift+R
    public static readonly HotkeyDef Default = new(
        GlobalHotkey.MOD_CONTROL | GlobalHotkey.MOD_SHIFT, 0x52);

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & GlobalHotkey.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((Modifiers & GlobalHotkey.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((Modifiers & GlobalHotkey.MOD_ALT) != 0) parts.Add("Alt");
            if ((Modifiers & GlobalHotkey.MOD_WIN) != 0) parts.Add("Win");

            var keyName = Vk switch
            {
                >= 0x30 and <= 0x39 => ((char)Vk).ToString(),     // 0-9
                >= 0x41 and <= 0x5A => ((char)Vk).ToString(),     // A-Z
                >= 0x70 and <= 0x87 => $"F{Vk - 0x70 + 1}",      // F1-F24
                0x20 => "Space",
                0x0D => "Enter",
                0x1B => "Esc",
                0x09 => "Tab",
                _ => $"0x{Vk:X2}"
            };
            parts.Add(keyName);
            return string.Join("+", parts);
        }
    }

    public string Serialize() => $"{Modifiers}:{Vk}";

    public static HotkeyDef Deserialize(string s)
    {
        var parts = s.Split(':');
        if (parts.Length == 2 &&
            uint.TryParse(parts[0], out var mod) &&
            uint.TryParse(parts[1], out var vk))
            return new HotkeyDef(mod, vk);
        return Default;
    }
}

// ─── Settings storage (DPAPI-encrypted) ─────────────────────────────────────

public static class AppSettings
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dictator");

    private static readonly string _keyPath = Path.Combine(_dir, "settings.dat");
    private static readonly string _hotkeyPath = Path.Combine(_dir, "hotkey.txt");

    public static string GetApiKey()
    {
        if (!File.Exists(_keyPath)) return "";
        try
        {
            var encrypted = File.ReadAllBytes(_keyPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted).Trim();
        }
        catch { return ""; }
    }

    public static void SetApiKey(string key)
    {
        Directory.CreateDirectory(_dir);
        var plain = Encoding.UTF8.GetBytes(key.Trim());
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_keyPath, encrypted);
    }

    public static bool HasApiKey() => !string.IsNullOrWhiteSpace(GetApiKey());

    public static HotkeyDef GetHotkey()
    {
        if (!File.Exists(_hotkeyPath)) return HotkeyDef.Default;
        try { return HotkeyDef.Deserialize(File.ReadAllText(_hotkeyPath).Trim()); }
        catch { return HotkeyDef.Default; }
    }

    public static void SetHotkey(HotkeyDef hk)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_hotkeyPath, hk.Serialize());
    }
}

// ─── Recognition history ─────────────────────────────────────────────────────

public record HistoryEntry(string Text, DateTime Timestamp);

public static class RecognitionHistory
{
    private const int MaxEntries = 30;
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dictator", "history.json");

    public static List<HistoryEntry> GetAll()
    {
        if (!File.Exists(_path)) return [];
        try { return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_path)) ?? []; }
        catch { return []; }
    }

    public static void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var list = GetAll();
            list.Add(new HistoryEntry(text, DateTime.Now));
            if (list.Count > MaxEntries)
                list.RemoveRange(0, list.Count - MaxEntries);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

// ─── Settings panel (used inside ContentDialog) ───────────────────────────────

public class SettingsPanel : Grid
{
    private readonly StackPanel _stack;
    private readonly TextBox _txtKey;
    private readonly TextBox _txtHotkey;
    private HotkeyDef _hotkey;

    public string ApiKey => _txtKey.Text.Trim();
    public HotkeyDef Hotkey => _hotkey;

    public SettingsPanel()
    {
        MaxHeight = 350;
        MinWidth  = 400;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        _stack = new StackPanel
        {
            Spacing = 8,
            Padding = new Thickness(0, 8, 0, 0)
        };
        scroll.Content = _stack;
        Children.Add(scroll);

        // ── API key ──
        _stack.Children.Add(new TextBlock
        {
            Text       = "API ключ Yandex Cloud:",
            Foreground = Brush(180, 180, 180),
            FontSize   = 13
        });

        _txtKey = new TextBox
        {
            Text            = AppSettings.GetApiKey(),
            PlaceholderText = "AQVN...",
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 12,
            Background      = Brush(40, 40, 40),
            Foreground      = Brush(230, 230, 230),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(8, 6, 8, 6)
        };
        _stack.Children.Add(_txtKey);

        _stack.Children.Add(new TextBlock
        {
            Text       = "Как получить ключ:",
            Foreground = Brush(160, 160, 160),
            FontSize   = 12,
            Margin     = new Thickness(0, 4, 0, 0)
        });

        foreach (var step in new[]
        {
            "1. Зайдите на aistudio.yandex.ru",
            "2. Создайте API-ключ в разделе ключей",
            "3. Вставьте ключ выше"
        })
        {
            _stack.Children.Add(new TextBlock { Text = step, Foreground = Brush(120, 120, 120), FontSize = 11 });
        }

        // ── Hotkey ──
        _stack.Children.Add(new TextBlock
        {
            Text       = "Горячая клавиша записи:",
            Foreground = Brush(180, 180, 180),
            FontSize   = 13,
            Margin     = new Thickness(0, 8, 0, 0)
        });

        _hotkey = AppSettings.GetHotkey();
        _txtHotkey = new TextBox
        {
            Text            = _hotkey.DisplayName,
            IsReadOnly      = true,
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 12,
            Background      = Brush(40, 40, 40),
            Foreground      = Brush(230, 230, 230),
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(8, 6, 8, 6),
            PlaceholderText = "Нажмите сочетание клавиш..."
        };
        _txtHotkey.PreviewKeyDown += OnHotkeyKeyDown;
        _stack.Children.Add(_txtHotkey);

        _stack.Children.Add(new TextBlock
        {
            Text       = "Кликните в поле и нажмите нужное сочетание клавиш",
            Foreground = Brush(120, 120, 120),
            FontSize   = 11
        });
    }

    private void OnHotkeyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;

        // Ignore standalone modifier presses
        var vk = (uint)e.Key;
        if (vk is 0xA0 or 0xA1  // LShift, RShift
            or 0xA2 or 0xA3     // LCtrl, RCtrl
            or 0xA4 or 0xA5     // LAlt, RAlt
            or 0x5B or 0x5C     // LWin, RWin
            or 16 or 17 or 18)  // Shift, Ctrl, Alt (generic)
            return;

        // Build modifiers from current keyboard state
        uint mod = 0;
        var ctrl  = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var alt   = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);

        if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))  mod |= GlobalHotkey.MOD_CONTROL;
        if (shift.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) mod |= GlobalHotkey.MOD_SHIFT;
        if (alt.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))   mod |= GlobalHotkey.MOD_ALT;

        _hotkey = new HotkeyDef(mod, vk);
        _txtHotkey.Text = _hotkey.DisplayName;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromArgb(255, r, g, b));
}
