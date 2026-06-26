using GHelper.Peripherals;
using GHelper.UI;
using GHelper.USB;
using System.Text.Json;

namespace GHelper;

public sealed class LightingStudio : RForm
{
    private sealed record KeyVisual(string Label, int Led, float X, float Y, float Width = 1, float Height = 1);

    private sealed class LightingPreset
    {
        public string Name { get; set; } = "";
        public int Mode { get; set; }
        public int Refresh { get; set; }
        public int Saturation { get; set; }
        public int Smooth { get; set; }
        public int Blur { get; set; } = 70;
        public int CropTop { get; set; }
        public int CropBottom { get; set; }
        public int BaseColor { get; set; }
        public string ZoneColors { get; set; } = "";
        public string PerKeyColors { get; set; } = "";
    }

    private sealed class AuraModeItem
    {
        public AuraMode Mode { get; init; }
        public string Text { get; init; } = "";
        public override string ToString() => Text;
    }

    private readonly SettingsForm settings;
    private readonly RComboBox modeCombo = new();
    private readonly NumericUpDown refreshInput = CreateNumber(50, 1000, 100, 50);
    private readonly NumericUpDown saturationInput = CreateNumber(0, 100, 20, 5);
    private readonly NumericUpDown smoothInput = CreateNumber(0, 95, 65, 5);
    private readonly NumericUpDown blurInput = CreateNumber(0, 100, 70, 5);
    private readonly NumericUpDown cropTopInput = CreateNumber(0, 70, 33, 1);
    private readonly NumericUpDown cropBottomInput = CreateNumber(0, 30, 2, 1);
    private readonly RButton baseColorButton = new();
    private readonly FlowLayoutPanel zonePanel = new();
    private readonly Panel keyboardPanel = new();
    private readonly ListBox presetList = new();
    private readonly TextBox presetName = new();
    private readonly Label statusLabel = new();
    private readonly CheckBox syncPeripherals = new();
    private readonly Label peripheralStatus = new();
    private readonly RButton keyboardBrushButton = new();
    private readonly Label keyboardSelectionLabel = new();
    private readonly Dictionary<int, RButton> keyButtons = new();

    private Color[] zoneColors = Aura.CustomRGB.GetCustomZoneColors();
    private Dictionary<int, Color> perKeyColors = Aura.CustomRGB.GetPerKeyColors();
    private Color baseColor = Aura.Color1;
    private Color keyboardBrushColor = Aura.Color1;

    private const float KeyboardUnitsWide = 19.25f;
    private const float KeyboardUnitsHigh = 7.6f;

    // Visual laptop keyboard layout. LED ids follow the ASUS per-key Aura map.
    private static readonly KeyVisual[] KeyboardKeys =
    [
        new("Vol -", 2, 0, 0, 1.2f), new("Vol +", 3, 1.3f, 0, 1.2f),
        new("Mic", 4, 2.6f, 0, 1.2f), new("Fan", 5, 3.9f, 0, 1.2f), new("ROG", 6, 5.2f, 0, 1.2f),

        new("Esc", 21, 0, 1.25f), new("F1", 23, 1.35f, 1.25f), new("F2", 24, 2.35f, 1.25f),
        new("F3", 25, 3.35f, 1.25f), new("F4", 26, 4.35f, 1.25f), new("F5", 28, 5.7f, 1.25f),
        new("F6", 29, 6.7f, 1.25f), new("F7", 30, 7.7f, 1.25f), new("F8", 31, 8.7f, 1.25f),
        new("F9", 33, 10.05f, 1.25f), new("F10", 34, 11.05f, 1.25f), new("F11", 35, 12.05f, 1.25f),
        new("F12", 36, 13.05f, 1.25f), new("Del", 37, 14.4f, 1.25f), new("Prt", 40, 15.4f, 1.25f),
        new("Home", 41, 16.4f, 1.25f),

        new("`", 42, 0, 2.35f), new("1", 43, 1, 2.35f), new("2", 44, 2, 2.35f),
        new("3", 45, 3, 2.35f), new("4", 46, 4, 2.35f), new("5", 47, 5, 2.35f),
        new("6", 48, 6, 2.35f), new("7", 49, 7, 2.35f), new("8", 50, 8, 2.35f),
        new("9", 51, 9, 2.35f), new("0", 52, 10, 2.35f), new("-", 53, 11, 2.35f),
        new("=", 54, 12, 2.35f), new("Backspace", 55, 13, 2.35f, 2.15f),

        new("Tab", 63, 0, 3.35f, 1.5f), new("Q", 64, 1.5f, 3.35f), new("W", 65, 2.5f, 3.35f),
        new("E", 66, 3.5f, 3.35f), new("R", 67, 4.5f, 3.35f), new("T", 68, 5.5f, 3.35f),
        new("Y", 69, 6.5f, 3.35f), new("U", 70, 7.5f, 3.35f), new("I", 71, 8.5f, 3.35f),
        new("O", 72, 9.5f, 3.35f), new("P", 73, 10.5f, 3.35f), new("[", 74, 11.5f, 3.35f),
        new("]", 75, 12.5f, 3.35f), new("\\", 76, 13.5f, 3.35f, 1.65f),

        new("Caps", 84, 0, 4.35f, 1.75f), new("A", 85, 1.75f, 4.35f), new("S", 86, 2.75f, 4.35f),
        new("D", 87, 3.75f, 4.35f), new("F", 88, 4.75f, 4.35f), new("G", 89, 5.75f, 4.35f),
        new("H", 90, 6.75f, 4.35f), new("J", 91, 7.75f, 4.35f), new("K", 92, 8.75f, 4.35f),
        new("L", 93, 9.75f, 4.35f), new(";", 94, 10.75f, 4.35f), new("'", 95, 11.75f, 4.35f),
        new("Enter", 97, 12.75f, 4.35f, 2.4f),

        new("Shift", 105, 0, 5.35f, 2.25f), new("Z", 107, 2.25f, 5.35f), new("X", 108, 3.25f, 5.35f),
        new("C", 109, 4.25f, 5.35f), new("V", 110, 5.25f, 5.35f), new("B", 111, 6.25f, 5.35f),
        new("N", 112, 7.25f, 5.35f), new("M", 113, 8.25f, 5.35f), new(",", 114, 9.25f, 5.35f),
        new(".", 115, 10.25f, 5.35f), new("/", 116, 11.25f, 5.35f), new("Shift", 117, 12.25f, 5.35f, 2.9f),
        new("Up", 139, 16.05f, 5.35f),

        new("Ctrl", 126, 0, 6.35f, 1.35f), new("Fn", 127, 1.35f, 6.35f), new("Win", 128, 2.35f, 6.35f),
        new("Alt", 129, 3.35f, 6.35f, 1.25f), new("Space", 131, 4.6f, 6.35f, 5.25f),
        new("Alt", 135, 9.85f, 6.35f, 1.25f), new("Fn", 136, 11.1f, 6.35f),
        new("Ctrl", 137, 12.1f, 6.35f, 1.45f), new("Left", 159, 15.05f, 6.35f),
        new("Down", 160, 16.05f, 6.35f), new("Right", 161, 17.05f, 6.35f),
    ];

    public LightingStudio(SettingsForm settings)
    {
        this.settings = settings;
        Text = "G-Helper Lighting Studio";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1080, 720);
        Size = new Size(1260, 820);
        Padding = new Padding(16);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildInterface();
        ReloadCurrentSettings();
        InitTheme(true);
        ApplyTheme();
        RefreshColorSurfaces();
        PeripheralsProvider.DeviceChanged += PeripheralsChanged;
        FormClosed += (_, _) => PeripheralsProvider.DeviceChanged -= PeripheralsChanged;
    }

    private static NumericUpDown CreateNumber(int min, int max, int value, int increment)
        => new() { Minimum = min, Maximum = max, Value = value, Increment = increment, Width = 110 };

    private void BuildInterface()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(shell);

        shell.Controls.Add(BuildHeader(), 0, 0);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 8, 0, 8) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildPresetPanel(), 0, 0);
        content.Controls.Add(BuildTabs(), 1, 0);
        shell.Controls.Add(content, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(5, 8, 5, 4)
        };
        footer.Controls.Add(MakeButton("Close", (_, _) => Close(), true));
        footer.Controls.Add(MakeButton("Apply lighting", (_, _) => ApplySettings()));
        statusLabel.AutoSize = true;
        statusLabel.Padding = new Padding(8, 10, 8, 0);
        footer.Controls.Add(statusLabel);
        shell.Controls.Add(footer, 0, 2);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12, 8, 12, 5) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        header.Controls.Add(new Label
        {
            Text = "LIGHTING STUDIO",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "Laptop + supported ASUS / ROG peripherals",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        }, 1, 0);
        return header;
    }

    private Control BuildPresetPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var title = new Label
        {
            Text = "PRESETS",
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        var hint = new Label
        {
            Text = "Save complete lighting setups and switch between them quickly.",
            Dock = DockStyle.Top,
            Height = 52
        };
        presetList.Dock = DockStyle.Fill;
        presetList.IntegralHeight = false;
        presetList.BorderStyle = BorderStyle.None;
        presetList.DoubleClick += (_, _) => LoadSelectedPreset();

        presetName.Dock = DockStyle.Bottom;
        presetName.Height = 34;
        presetName.PlaceholderText = "New preset name";

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 92,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        actions.Controls.Add(MakeButton("Save", (_, _) => SavePreset()));
        actions.Controls.Add(MakeButton("Load", (_, _) => LoadSelectedPreset(), true));
        actions.Controls.Add(MakeButton("Delete", (_, _) => DeleteSelectedPreset(), true));

        panel.Controls.Add(presetList);
        panel.Controls.Add(actions);
        panel.Controls.Add(presetName);
        panel.Controls.Add(hint);
        panel.Controls.Add(title);
        RefreshPresetList();
        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 7) };
        tabs.TabPages.Add(BuildEffectsTab());
        tabs.TabPages.Add(BuildZoneTab());
        tabs.TabPages.Add(BuildPerKeyTab());
        return tabs;
    }

    private TabPage BuildEffectsTab()
    {
        var page = new TabPage("Effects") { Padding = new Padding(18), AutoScroll = true };
        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        columns.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        modeCombo.Items.Clear();
        foreach ((AuraMode mode, string text) in Aura.GetModes())
            modeCombo.Items.Add(new AuraModeItem { Mode = mode, Text = text });

        baseColorButton.Text = "Choose color";
        baseColorButton.MinimumSize = new Size(145, 40);
        baseColorButton.Click += (_, _) => PickBaseColor();

        var primary = BuildCard("LIGHTING", "Choose an effect and its main color.");
        AddSetting((TableLayoutPanel)primary.Tag!, "Effect", "Built-in or advanced lighting engine.", modeCombo);
        AddSetting((TableLayoutPanel)primary.Tag!, "Base color", "Static color and fallback for unpainted keys.", baseColorButton);
        FinalizeCard(primary);

        var ambient = BuildCard("AMBIENT ENGINE", "Tune how screen colors are sampled.");
        AddSetting((TableLayoutPanel)ambient.Tag!, "Refresh", "Sampling interval in milliseconds.", refreshInput);
        AddSetting((TableLayoutPanel)ambient.Tag!, "Saturation", "Increase sampled color intensity.", saturationInput);
        AddSetting((TableLayoutPanel)ambient.Tag!, "Temporal smoothing", "Higher values make color changes slower and calmer.", smoothInput);
        AddSetting((TableLayoutPanel)ambient.Tag!, "Spatial blur", "0 samples near each zone center; 100 averages the full zone.", blurInput);
        AddSetting((TableLayoutPanel)ambient.Tag!, "Top crop", "Ignore this percentage from the top.", cropTopInput);
        AddSetting((TableLayoutPanel)ambient.Tag!, "Bottom crop", "Ignore the taskbar-side percentage.", cropBottomInput);
        FinalizeCard(ambient);

        syncPeripherals.Text = "Sync supported ASUS / ROG peripherals";
        syncPeripherals.AutoSize = true;
        peripheralStatus.AutoSize = true;
        peripheralStatus.MaximumSize = new Size(330, 0);

        var devices = BuildCard("PERIPHERALS", "G-Helper only writes to explicitly supported ASUS/ROG hardware.");
        AddSetting((TableLayoutPanel)devices.Tag!, "Aura sync", "Mirror laptop lighting to supported external devices.", syncPeripherals);
        AddSetting((TableLayoutPanel)devices.Tag!, "Detected", "Available external lighting devices.", peripheralStatus);
        FinalizeCard(devices);

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        left.Controls.Add(primary);
        left.Controls.Add(devices);
        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        right.Controls.Add(ambient);
        columns.Controls.Add(left, 0, 0);
        columns.Controls.Add(right, 1, 0);
        page.Controls.Add(columns);
        return page;
    }

    private Panel BuildCard(string title, string description)
    {
        var card = new Panel { Width = 430, Padding = new Padding(16), Margin = new Padding(6) };
        var layout = new TableLayoutPanel
        {
            Location = new Point(16, 72),
            Width = 398,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        var heading = new Label
        {
            Text = $"{title}\n{description}",
            Location = new Point(16, 14),
            Width = 398,
            Height = 58,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        card.Controls.Add(heading);
        card.Controls.Add(layout);
        card.Tag = layout;
        return card;
    }

    private static void FinalizeCard(Panel card)
    {
        var layout = (TableLayoutPanel)card.Tag!;
        card.Height = 96 + layout.RowCount * 68;
    }

    private TabPage BuildZoneTab()
    {
        var page = new TabPage("Zones") { Padding = new Padding(22) };
        page.Controls.Add(new Label
        {
            Text = "LAPTOP ZONES - Click a segment to choose its color",
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        });
        zonePanel.Dock = DockStyle.Top;
        zonePanel.Height = 255;
        zonePanel.Padding = new Padding(12, 18, 12, 8);
        page.Controls.Add(zonePanel);
        BuildZoneButtons();
        return page;
    }

    private TabPage BuildPerKeyTab()
    {
        var page = new TabPage("Per-key keyboard") { Padding = new Padding(16) };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            Text = Aura.BacklightType == AuraBacklightType.PerKey
                ? "PER-KEY KEYBOARD - Choose a brush color, then paint keys"
                : "This laptop was not detected as a per-key RGB keyboard.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        root.SetColumnSpan(root.GetControlFromPosition(0, 0)!, 2);

        keyboardPanel.Dock = DockStyle.Fill;
        keyboardPanel.Padding = new Padding(12);
        keyboardPanel.Resize += (_, _) => LayoutKeyboard();
        keyboardPanel.Enabled = Aura.BacklightType == AuraBacklightType.PerKey;
        root.Controls.Add(keyboardPanel, 0, 1);

        var tools = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12, 18, 8, 8)
        };
        tools.Controls.Add(new Label
        {
            Text = "PAINT TOOLS",
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(4, 0, 4, 10)
        });
        keyboardBrushButton.Text = "Brush color";
        keyboardBrushButton.Size = new Size(165, 44);
        keyboardBrushButton.Click += (_, _) => PickKeyboardBrush();
        tools.Controls.Add(keyboardBrushButton);
        tools.Controls.Add(MakeButton("Paint all keys", (_, _) => PaintAllKeys()));
        tools.Controls.Add(MakeButton("Clear overrides", (_, _) => ClearKeyOverrides(), true));
        keyboardSelectionLabel.Text = "Click any key to paint it.";
        keyboardSelectionLabel.AutoSize = true;
        keyboardSelectionLabel.MaximumSize = new Size(170, 0);
        keyboardSelectionLabel.Margin = new Padding(5, 16, 5, 5);
        tools.Controls.Add(keyboardSelectionLabel);
        tools.Controls.Add(new Label
        {
            Text = "The five keys at the top represent dedicated laptop controls when the detected LED map exposes them.",
            AutoSize = true,
            MaximumSize = new Size(170, 0),
            Margin = new Padding(5, 20, 5, 5)
        });
        root.Controls.Add(tools, 1, 1);

        page.Controls.Add(root);
        BuildKeyboard();
        return page;
    }

    private void BuildZoneButtons()
    {
        zonePanel.Controls.Clear();
        zonePanel.FlowDirection = FlowDirection.LeftToRight;
        zonePanel.WrapContents = true;
        zonePanel.AutoScroll = false;
        string[] names = ["Keyboard left", "Keyboard mid-left", "Keyboard mid-right", "Keyboard right", "Lightbar left", "Lightbar mid-left", "Lightbar mid-right", "Lightbar right"];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = MakeButton(names[i], (_, _) => PickZoneColor(index));
            button.Size = new Size(i < 4 ? 205 : 190, i < 4 ? 96 : 72);
            button.Margin = new Padding(i == 4 ? 28 : 5, i < 4 ? 12 : 2, 5, 5);
            button.BackColor = zoneColors[i];
            button.ForeColor = GetContrast(zoneColors[i]);
            button.Tag = index;
            zonePanel.Controls.Add(button);
        }
    }

    private void BuildKeyboard()
    {
        keyboardPanel.Controls.Clear();
        keyButtons.Clear();
        foreach (KeyVisual key in KeyboardKeys)
        {
            int led = key.Led;
            KeyVisual visual = StandardKeyboardVisual(key);
            var button = MakeButton(visual.Label, (_, _) => PaintKey(led), true);
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.Tag = visual;
            keyButtons[led] = button;
            keyboardPanel.Controls.Add(button);
        }
        LayoutKeyboard();
        RefreshKeyboardColors();
    }

    private static KeyVisual StandardKeyboardVisual(KeyVisual key)
    {
        return key.Led switch
        {
            2 => key with { Label = "Vol-", X = 12.15f, Y = 0, Width = 1.05f },
            3 => key with { Label = "Vol+", X = 13.25f, Y = 0, Width = 1.05f },
            4 => key with { Label = "Mic", X = 14.35f, Y = 0, Width = 1.05f },
            5 => key with { Label = "Fan", X = 15.45f, Y = 0, Width = 1.05f },
            6 => key with { Label = "ROG", X = 16.55f, Y = 0, Width = 1.15f },
            21 => key with { X = 0, Y = 1.15f },
            23 => key with { X = 1.35f, Y = 1.15f },
            24 => key with { X = 2.35f, Y = 1.15f },
            25 => key with { X = 3.35f, Y = 1.15f },
            26 => key with { X = 4.35f, Y = 1.15f },
            28 => key with { X = 5.7f, Y = 1.15f },
            29 => key with { X = 6.7f, Y = 1.15f },
            30 => key with { X = 7.7f, Y = 1.15f },
            31 => key with { X = 8.7f, Y = 1.15f },
            33 => key with { X = 10.05f, Y = 1.15f },
            34 => key with { X = 11.05f, Y = 1.15f },
            35 => key with { X = 12.05f, Y = 1.15f },
            36 => key with { X = 13.05f, Y = 1.15f },
            37 => key with { X = 15.4f, Y = 1.15f },
            40 => key with { X = 14.4f, Y = 1.15f },
            41 => key with { X = 16.4f, Y = 1.15f },
            53 => key with { Label = "-" },
            139 => key with { Label = "Up" },
            159 => key with { Label = "Left" },
            160 => key with { Label = "Down" },
            161 => key with { Label = "Right" },
            _ => key
        };
    }

    private void LayoutKeyboard()
    {
        if (keyboardPanel.ClientSize.Width <= 40 || keyboardPanel.ClientSize.Height <= 40) return;
        float unit = Math.Min((keyboardPanel.ClientSize.Width - 24) / KeyboardUnitsWide,
            (keyboardPanel.ClientSize.Height - 24) / KeyboardUnitsHigh);
        float originX = Math.Max(8, (keyboardPanel.ClientSize.Width - KeyboardUnitsWide * unit) / 2);
        float originY = Math.Max(8, (keyboardPanel.ClientSize.Height - KeyboardUnitsHigh * unit) / 2);
        int gap = Math.Max(2, (int)(unit * 0.06f));

        foreach (RButton button in keyButtons.Values)
        {
            KeyVisual key = (KeyVisual)button.Tag!;
            button.Bounds = new Rectangle(
                (int)(originX + key.X * unit),
                (int)(originY + key.Y * unit),
                Math.Max(24, (int)(key.Width * unit) - gap),
                Math.Max(24, (int)(key.Height * unit) - gap));
            button.Font = new Font("Segoe UI", Math.Clamp(unit * 0.18f, 7.5f, 10.5f), FontStyle.Regular);
        }
    }

    private void AddSetting(TableLayoutPanel layout, string title, string description, Control input)
    {
        int row = layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        var text = new Label { Dock = DockStyle.Fill, Text = $"{title}\n{description}", Padding = new Padding(0, 8, 10, 0) };
        input.Anchor = AnchorStyles.Right;
        layout.Controls.Add(text, 0, row);
        layout.Controls.Add(input, 1, row);
    }

    private RButton MakeButton(string text, EventHandler onClick, bool secondary = false)
    {
        var button = new RButton
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(86, 38),
            Margin = new Padding(4),
            Secondary = secondary,
            BorderRadius = 4
        };
        button.Click += onClick;
        return button;
    }

    public void ReloadCurrentSettings()
    {
        AuraMode currentMode = (AuraMode)AppConfig.Get("aura_mode", (int)settings.GetCurrentAuraMode());
        if (!Aura.GetModes().ContainsKey(currentMode))
            currentMode = settings.GetCurrentAuraMode();
        if (!Aura.GetModes().ContainsKey(currentMode))
            currentMode = AuraMode.AuraStatic;

        SelectAuraMode(currentMode);
        refreshInput.Value = Math.Clamp(AppConfig.Get("aura_refresh", AppConfig.IsStrix() ? 100 : 300), 50, 1000);
        saturationInput.Value = Math.Clamp(AppConfig.Get("aura_ambient_saturation", 20), 0, 100);
        smoothInput.Value = Math.Clamp(AppConfig.Get("aura_ambient_smooth", 65), 0, 95);
        blurInput.Value = Math.Clamp(AppConfig.Get("aura_ambient_blur", 70), 0, 100);
        cropTopInput.Value = Math.Clamp(AppConfig.Get("aura_ambient_crop_top", 33), 0, 70);
        cropBottomInput.Value = Math.Clamp(AppConfig.Get("aura_ambient_crop_bottom", 2), 0, 30);
        syncPeripherals.Checked = PeripheralsProvider.IsAuraSync;

        baseColor = Color.FromArgb(AppConfig.Get("aura_color", settings.GetCurrentAuraColor().ToArgb()));
        zoneColors = Aura.CustomRGB.GetCustomZoneColors();
        perKeyColors = Aura.CustomRGB.GetPerKeyColors();
        keyboardBrushColor = baseColor;
        RefreshColorSurfaces();
        RefreshPeripheralStatus();
        statusLabel.Text = "Current settings loaded";
    }

    private void SelectAuraMode(AuraMode mode)
    {
        foreach (object? item in modeCombo.Items)
        {
            if (item is AuraModeItem modeItem && modeItem.Mode == mode)
            {
                modeCombo.SelectedItem = item;
                return;
            }
        }

        if (modeCombo.Items.Count > 0)
            modeCombo.SelectedIndex = 0;
    }

    private AuraMode SelectedAuraMode()
    {
        return modeCombo.SelectedItem is AuraModeItem item ? item.Mode : AuraMode.AuraStatic;
    }

    private void ApplySettings()
    {
        AuraMode selectedMode = SelectedAuraMode();
        AppConfig.Set("aura_mode", (int)selectedMode);
        AppConfig.Set("aura_refresh", (int)refreshInput.Value);
        AppConfig.Set("aura_ambient_saturation", (int)saturationInput.Value);
        AppConfig.Set("aura_ambient_smooth", (int)smoothInput.Value);
        AppConfig.Set("aura_ambient_blur", (int)blurInput.Value);
        AppConfig.Set("aura_ambient_crop_top", (int)cropTopInput.Value);
        AppConfig.Set("aura_ambient_crop_bottom", (int)cropBottomInput.Value);
        AppConfig.Set("aura_color", baseColor.ToArgb());
        Aura.SetColor(baseColor.ToArgb());
        Aura.CustomRGB.SetCustomZoneColors(zoneColors);
        Aura.CustomRGB.SetPerKeyColors(perKeyColors);
        PeripheralsProvider.SetAuraSync(syncPeripherals.Checked);
        settings.InitAuraSelection(selectedMode);
        settings.SetAura();
        PeripheralsProvider.SyncPeripheralsWithKeyboardAura();
        settings.UpdateKeyboardLabel();
        statusLabel.Text = "Lighting applied";
    }

    private void PickZoneColor(int index)
    {
        Color? color = PickColor(zoneColors[index]);
        if (color is null) return;
        zoneColors[index] = color.Value;
        BuildZoneButtons();
        SelectAuraMode(AuraMode.CUSTOMZONE);
    }

    private void PickBaseColor()
    {
        Color? color = PickColor(baseColor);
        if (color is null) return;
        baseColor = color.Value;
        RefreshColorSurfaces();
    }

    private void PickKeyboardBrush()
    {
        Color? color = PickColor(keyboardBrushColor);
        if (color is null) return;
        keyboardBrushColor = color.Value;
        RefreshKeyboardBrush();
    }

    private void PaintKey(int led)
    {
        perKeyColors[led] = keyboardBrushColor;
        RefreshKeyboardColors();
        keyboardSelectionLabel.Text = $"{KeyboardKeys.First(key => key.Led == led).Label} painted";
        SelectAuraMode(AuraMode.PERKEY);
    }

    private void PaintAllKeys()
    {
        foreach (KeyVisual key in KeyboardKeys)
            perKeyColors[key.Led] = keyboardBrushColor;
        RefreshKeyboardColors();
        keyboardSelectionLabel.Text = "All visible keys painted";
        SelectAuraMode(AuraMode.PERKEY);
    }

    private void ClearKeyOverrides()
    {
        perKeyColors.Clear();
        RefreshKeyboardColors();
        keyboardSelectionLabel.Text = "Overrides cleared; base color is shown";
    }

    private void RefreshKeyboardColors()
    {
        foreach ((int led, RButton button) in keyButtons)
        {
            Color color = perKeyColors.TryGetValue(led, out Color selected) ? selected : baseColor;
            button.BackColor = color;
            button.ForeColor = GetContrast(color);
        }
    }

    private void RefreshKeyboardBrush()
    {
        keyboardBrushButton.BackColor = keyboardBrushColor;
        keyboardBrushButton.ForeColor = GetContrast(keyboardBrushColor);
    }

    private static Color? PickColor(Color current)
    {
        using var dialog = new ColorDialog { AllowFullOpen = true, FullOpen = true, Color = current };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.Color : null;
    }

    private static Color GetContrast(Color color)
        => color.GetBrightness() > 0.55f ? Color.Black : Color.White;

    private List<LightingPreset> GetPresets()
    {
        try
        {
            return JsonSerializer.Deserialize<List<LightingPreset>>(AppConfig.GetString("lighting_presets", "[]")) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void RefreshPresetList()
    {
        string? selected = presetList.SelectedItem?.ToString();
        presetList.Items.Clear();
        foreach (LightingPreset preset in GetPresets())
            presetList.Items.Add(preset.Name);
        if (selected is not null)
            presetList.SelectedItem = selected;
    }

    private void SavePreset()
    {
        string name = presetName.Text.Trim();
        if (name.Length == 0)
        {
            statusLabel.Text = "Enter a preset name";
            return;
        }

        List<LightingPreset> presets = GetPresets();
        presets.RemoveAll(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        presets.Add(new LightingPreset
        {
            Name = name,
            Mode = (int)SelectedAuraMode(),
            Refresh = (int)refreshInput.Value,
            Saturation = (int)saturationInput.Value,
            Smooth = (int)smoothInput.Value,
            Blur = (int)blurInput.Value,
            CropTop = (int)cropTopInput.Value,
            CropBottom = (int)cropBottomInput.Value,
            BaseColor = baseColor.ToArgb(),
            ZoneColors = string.Join(",", zoneColors.Select(color => color.ToArgb())),
            PerKeyColors = string.Join(",", perKeyColors.Select(pair => $"{pair.Key}:{pair.Value.ToArgb()}"))
        });
        AppConfig.Set("lighting_presets", JsonSerializer.Serialize(presets));
        RefreshPresetList();
        presetList.SelectedItem = name;
        statusLabel.Text = "Preset saved";
    }

    private void LoadSelectedPreset()
    {
        string? name = presetList.SelectedItem?.ToString();
        LightingPreset? preset = GetPresets().FirstOrDefault(item => item.Name == name);
        if (preset is null) return;

        SelectAuraMode((AuraMode)preset.Mode);
        refreshInput.Value = Math.Clamp(preset.Refresh, 50, 1000);
        saturationInput.Value = Math.Clamp(preset.Saturation, 0, 100);
        smoothInput.Value = Math.Clamp(preset.Smooth, 0, 95);
        blurInput.Value = Math.Clamp(preset.Blur, 0, 100);
        cropTopInput.Value = Math.Clamp(preset.CropTop, 0, 70);
        cropBottomInput.Value = Math.Clamp(preset.CropBottom, 0, 30);
        baseColor = Color.FromArgb(preset.BaseColor);
        keyboardBrushColor = baseColor;

        zoneColors = preset.ZoneColors.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out int argb) ? Color.FromArgb(argb) : baseColor)
            .Concat(Enumerable.Repeat(baseColor, Aura.CustomRGB.ZoneCount))
            .Take(Aura.CustomRGB.ZoneCount).ToArray();

        perKeyColors = [];
        foreach (string pair in preset.PerKeyColors.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int led) && int.TryParse(parts[1], out int argb))
                perKeyColors[led] = Color.FromArgb(argb);
        }

        RefreshColorSurfaces();
        statusLabel.Text = "Preset loaded";
    }

    private void DeleteSelectedPreset()
    {
        string? name = presetList.SelectedItem?.ToString();
        if (name is null) return;
        List<LightingPreset> presets = GetPresets();
        presets.RemoveAll(preset => preset.Name == name);
        AppConfig.Set("lighting_presets", JsonSerializer.Serialize(presets));
        RefreshPresetList();
        statusLabel.Text = "Preset deleted";
    }

    private void ApplyTheme()
    {
        BackColor = formBack;
        ForeColor = foreMain;
        ApplyThemeRecursive(this);
    }

    private void RefreshColorSurfaces()
    {
        baseColorButton.BackColor = baseColor;
        baseColorButton.ForeColor = GetContrast(baseColor);
        RefreshKeyboardBrush();
        BuildZoneButtons();
        RefreshKeyboardColors();
    }

    private void ApplyThemeRecursive(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            control.ForeColor = foreMain;
            if (control is TabPage or TableLayoutPanel or FlowLayoutPanel)
                control.BackColor = formBack;
            else if (control is Panel panel)
                panel.BackColor = ReferenceEquals(panel, keyboardPanel) ? buttonSecond : formBack;
            else if (control is RButton button && !keyButtons.ContainsValue(button)
                     && !ReferenceEquals(button, baseColorButton) && !ReferenceEquals(button, keyboardBrushButton))
                button.BackColor = button.Secondary ? buttonSecond : buttonMain;
            else if (control is TextBox or ListBox or NumericUpDown or ComboBox)
                control.BackColor = buttonMain;
            ApplyThemeRecursive(control);
        }
    }

    private void PeripheralsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(RefreshPeripheralStatus);
        else
            RefreshPeripheralStatus();
    }

    private void RefreshPeripheralStatus()
    {
        IPeripheral[] devices = PeripheralsProvider.AllPeripherals().ToArray();
        string syncState = PeripheralsProvider.IsAuraSync ? "Sync on" : "Sync off";

        if (devices.Length == 0)
        {
            peripheralStatus.Text = $"{syncState}; no supported external lighting devices detected";
            return;
        }

        int keyboards = devices.Count(device => device.DeviceType() == PeripheralType.Keyboard);
        int mice = devices.Count(device => device.DeviceType() == PeripheralType.Mouse);
        string[] names = devices
            .Select(device => device.GetDisplayName() + (device.IsDeviceReady ? "" : " (not ready)"))
            .Distinct()
            .ToArray();

        peripheralStatus.Text = $"{syncState}; {Plural(keyboards, "keyboard")}, {Plural(mice, "mouse")}: {string.Join(", ", names)}";
    }

    private static string Plural(int count, string name)
        => $"{count} {name}{(count == 1 ? "" : "s")}";
}
