using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using GHelper.AnimeMatrix.Communication.Platform;
using GHelper.Peripherals;
using GHelper.Peripherals.Keyboard;
using GHelper.Peripherals.Keyboard.Models;
using GHelper.USB;

// Exercises the compiled production classes with a memory-only USB transport.
// Never calls Connect, SetProvider, ApplyAura, screen capture, or GHelper.Program.
static class SpatialSyncChecks
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly FieldInfo Worker = typeof(AsusKeyboard).GetField("_auraFrameWorkerRunning", Private)!;
    static readonly FieldInfo FrameLock = typeof(AsusKeyboard).GetField("_auraFrameLock", Private)!;
    static int passed;
    static readonly Color[] Zones = [Color.Red, Color.Lime, Color.Cyan, Color.Blue];

    public static void Run()
    {
        string local = AppContext.BaseDirectory;
        File.WriteAllText(Path.Combine(local, "config.json"), "{}");
        Logger.appPath = local;
        Logger.logFile = Path.Combine(local, "spatial-test.log");
        InitializeIsolatedConfig(local);
        RunTests();
        Console.WriteLine($"PASS: {passed} spatial-sync checks; memory-only USB, no hardware I/O or screen capture.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void InitializeIsolatedConfig(string local)
    {
        AppConfig.Get("test"); // Explicit static constructor reads the local startup config.
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        string actual = (string)typeof(AppConfig).GetField("configFile", flags)!.GetValue(null)!;
        Check(Path.GetFullPath(actual) == Path.Combine(local, "config.json"), "test config must be local");
        // AppConfig normally mirrors changes into ProgramData. Disable that for ALL tests.
        typeof(AppConfig).GetField("fallbackConfigFile", flags)!.SetValue(null, actual);
    }

    static void RunTests()
    {
        var kb = new FakeClaymore();
        PeripheralsProvider.ConnectedKeyboards.Add(kb);
        SetMode(AuraMode.AMBIENT);
        PeripheralsProvider.SetKeyboardAuraSync(true);

        Test("Claymore II wire addresses match independent device reference", () =>
        {
            Send(kb, Zones);
            ValidateClaymoreIIWireMap(kb, false);
            kb.Usb.Clear();
        });

        Test("left-mounted numpad shifts main LEDs and covers all eight reported keys", () =>
        {
            kb.NumpadOnLeft = true;
            Send(kb, Zones);
            ValidateClaymoreIIWireMap(kb, true);
            ValidateFrame(kb, "ClaymoreIIMainLeft", 100);
            byte[] previouslyMissing = [0x71, 0x79, 0x72, 0x7A, 0x63, 0x73, 0x5C, 0x64];
            var sent = Decode(kb.Usb.Packets).Select(key => key.Id).ToHashSet();
            Check(previouslyMissing.All(sent.Contains), "9/0/O/P/J/L/N/M left-layout addresses all sent");
            Check(new ClaymoreII().NumpadOnLeft, "wired and wireless identities share the physical layout selection");
            kb.Usb.Clear();
        });

        Test("changing numpad side replays the cached Ambient frame with the new map", () =>
        {
            kb.NumpadOnLeft = false;
            Check(kb.SyncFromLaptopAura(), "current ambient frame replayed");
            Idle(kb);
            ValidateClaymoreIIWireMap(kb, false);
            kb.Usb.Clear();
        });

        Test("ISO Enter and extra keys have dedicated left/right addresses", () =>
        {
            foreach (var side in new[] { false, true })
            {
                kb.NumpadOnLeft = side;
                string layoutName = side ? "ClaymoreIIMainLeftISO" : "ClaymoreIIMainRightISO";
                kb.ForcedLayoutName = layoutName;
                Send(kb, Zones);
                ValidateFrame(kb, layoutName, 100);
                var names = kb.KeyLayout().SelectMany(row => row).Select(key => key.Name).ToArray();
                var keys = Decode(kb.Usb.Packets);
                int offset = side ? 0x20 : 0;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == "Enter") Check(keys[i].Id == 0x7B + offset, "ISO Enter shares one LED across UI rows");
                    if (names[i] == "#") Check(keys[i].Id == 0x6B + offset, "ISO hash address");
                    if (names[i] == "<") Check(keys[i].Id == 0x0C + offset, "ISO extra shift-row key address");
                }
                kb.Usb.Clear();
            }
            kb.ForcedLayoutName = null;
            kb.NumpadOnLeft = false;
        });

        Test("US geometry, LED map, chunk sizes and ACK consumption", () =>
        {
            Send(kb, Zones);
            ValidateFrame(kb, "ClaymoreIIMainRight", 100);
            Check(kb.Usb.Reads == kb.Usb.Packets.Count, "each spatial packet must consume one ACK");
            Check(Decode(kb.Usb.Packets).Select(x => x.Color.ToArgb()).Distinct().Count() > 20, "smooth interpolation");
        });

        Test("lightbar colors never enter main keyboard mapping", () =>
        {
            var reference = kb.Usb.Packets.SelectMany(x => x).ToArray();
            kb.Usb.Clear();
            Send(kb, [.. Zones, Color.Magenta, Color.White, Color.Yellow, Color.Black]);
            Check(reference.SequenceEqual(kb.Usb.Packets.SelectMany(x => x)), "ignore last four zones");
        });

        Test("ISO geometry and existing map remain valid", () =>
        {
            kb.ForcedLayoutName = "ClaymoreIIMainRightISO";
            kb.Usb.Clear();
            Send(kb, Zones);
            ValidateFrame(kb, "ClaymoreIIMainRightISO", 100);
            kb.ForcedLayoutName = null;
        });

        Test("external brightness scales every channel", () =>
        {
            kb.StoreLighting(KeyboardLightingMode.Static, Color.Red, Color.Blue, Color.Black, AuraSpeed.Normal, 37);
            kb.Usb.Clear();
            Send(kb, Zones);
            ValidateFrame(kb, "ClaymoreIIMainRight", 37);
            kb.StoreLighting(KeyboardLightingMode.Static, Color.Red, Color.Blue, Color.Black, AuraSpeed.Normal, 100);
        });

        Test("Gradient enable/hotplug path uses current colors, not static/save", () =>
        {
            SetMode(AuraMode.GRADIENT);
            AppConfig.Set("aura_color", Color.Red.ToArgb());
            AppConfig.Set("aura_color2", Color.Blue.ToArgb());
            kb.Usb.Clear();
            Check(kb.SyncFromLaptopAura(), "gradient sync accepted");
            Idle(kb);
            var keys = Decode(kb.Usb.Packets);
            Check(keys[0].Color.ToArgb() == Color.Blue.ToArgb(), "gradient left = laptop color2");
            Check(keys[15].Color.ToArgb() == Color.Red.ToArgb(), "gradient right = laptop color1");
            Check(keys.All(x => x.Color.G == 0), "red/blue gradient channels");
            Check(keys.Select(x => x.Color.ToArgb()).Distinct().Count() > 20, "gradient spatial, not solid");
        });

        Test("Ambient enable replays immutable cached frame on a static screen", () =>
        {
            SetMode(AuraMode.AMBIENT);
            PeripheralsProvider.SetKeyboardAuraSync(false);
            kb.Usb.Clear();
            Color[] source = (Color[])Zones.Clone();
            PeripheralsProvider.StreamKeyboardColors(source);
            Array.Fill(source, Color.White);
            Check(kb.Usb.Packets.Count == 0, "disabled sync does not write");
            PeripheralsProvider.SetKeyboardAuraSync(true);
            Check(kb.SyncFromLaptopAura(), "ambient cache replay accepted");
            Idle(kb);
            ValidateFrame(kb, "ClaymoreIIMainRight", 100);
        });

        Test("slow USB coalesces 2000 updates to the latest immutable frame", () =>
        {
            kb.Usb.Clear();
            kb.Usb.BlockNextWrite();
            PeripheralsProvider.StreamKeyboardColors(Zones);
            Check(kb.Usb.Entered.Wait(3000), "worker started");
            try
            {
                for (int i = 0; i < 2000; i++) PeripheralsProvider.StreamKeyboardColors([Color.Red, Color.Red, Color.Red, Color.Red]);
                Color[] final = [Color.Green, Color.Green, Color.Green, Color.Green];
                PeripheralsProvider.StreamKeyboardColors(final);
                Array.Fill(final, Color.White);
            }
            finally { kb.Usb.Release.Set(); }
            Idle(kb);
            int chunks = (kb.KeyLayout().Sum(row => row.Length) + 13) / 14;
            Check(kb.Usb.Packets.Count == chunks * 2, "exactly in-flight + latest pending frame");
            Check(Decode(kb.Usb.Packets.Skip(chunks)).All(x => x.Color.ToArgb() == Color.Green.ToArgb()), "latest frame and cloned input");
        });

        Test("disable/re-enable invalidates old queued frames", () =>
        {
            kb.Usb.Clear();
            kb.Usb.BlockNextWrite();
            PeripheralsProvider.StreamKeyboardColors(Zones);
            Check(kb.Usb.Entered.Wait(3000), "worker started");
            try
            {
                PeripheralsProvider.StreamKeyboardColors([Color.White, Color.White, Color.White, Color.White]);
                PeripheralsProvider.SetKeyboardAuraSync(false);
                PeripheralsProvider.SetKeyboardAuraSync(true);
            }
            finally { kb.Usb.Release.Set(); }
            Idle(kb);
            Check(kb.Usb.Packets.Count == 1, "stale in-flight tail and pending frame cancelled");
        });

        Test("native mode switch cannot be overwritten by stale spatial frames", () =>
        {
            kb.Usb.Clear();
            kb.Usb.BlockNextWrite();
            PeripheralsProvider.StreamKeyboardColors(Zones);
            Check(kb.Usb.Entered.Wait(3000), "worker started");
            Task<bool> native;
            try
            {
                PeripheralsProvider.StreamKeyboardColors(Zones);
                SetMode(AuraMode.AuraBreathe);
                native = Task.Run(kb.SyncFromLaptopAura);
            }
            finally { kb.Usb.Release.Set(); }
            Check(native.Wait(3000) && native.Result, "native mode sync completes");
            Idle(kb);
            Check(kb.Usb.Packets.Count == 3, "one partial spatial packet + native mode + save");
            Check(kb.Usb.Packets[1][1] == 0x51 && kb.Usb.Packets[1][3] == (byte)KeyboardLightingMode.Breathing, "native breathing preserved");
            Check(kb.Usb.Packets[2][1] == 0x50 && kb.Usb.Packets[2][2] == 0x55, "native save preserved");
        });

        Test("USB timeout and negative ACK do not strand future frames", () =>
        {
            SetMode(AuraMode.AMBIENT);
            foreach (bool timeout in new[] { true, false })
            {
                kb.Usb.Clear();
                kb.Usb.ThrowNextRead = timeout;
                kb.Usb.RejectNextRead = !timeout;
                Send(kb, Zones);
                Check(kb.Usb.Packets.Count == 1, "failed frame stops immediately");
                kb.Usb.Clear();
                Send(kb, Zones);
                ValidateFrame(kb, "ClaymoreIIMainRight", 100);
            }
        });

        Test("unsupported keyboard models keep their single-color fallback", () =>
        {
            var fallback = new FakeUnsupported();
            PeripheralsProvider.ConnectedKeyboards.Add(fallback);
            Send(kb, Zones);
            Idle(fallback);
            Check(fallback.Usb.Packets.Count == 1 && fallback.Usb.Packets[0][1] == 0x51, "fallback uses native static packet");
            Check(fallback.Usb.Packets[0][10] == Color.Blue.R && fallback.Usb.Packets[0][11] == Color.Blue.G && fallback.Usb.Packets[0][12] == Color.Blue.B, "fallback uses fourth zone");
            PeripheralsProvider.ConnectedKeyboards.Remove(fallback);
        });

        Test("disconnected/TestMode keyboards do not write", () =>
        {
            kb.Usb.Clear();
            kb.Ready(false);
            Send(kb, Zones);
            kb.Ready(true);
            kb.TestMode = true;
            Send(kb, Zones);
            kb.TestMode = false;
            Check(kb.Usb.Packets.Count == 0, "no USB writes");
        });

        Test("manual per-key protocol still uses existing mapped packets", () =>
        {
            kb.Usb.Clear();
            Color[] colors = Enumerable.Repeat(Color.Orange, kb.KeyLayout().Sum(x => x.Length)).ToArray();
            Check(kb.SetLedColors(colors), "manual direct colors accepted");
            var keys = Decode(kb.Usb.Packets);
            Check(keys.Select(x => x.Id).SequenceEqual(AuraKeyboardLayouts.Data["ClaymoreIIMainRight"].LedIds), "manual map unchanged");
            Check(keys.All(x => x.Color.ToArgb() == Color.Orange.ToArgb()), "manual colors unchanged");
        });

        Test("new Ambient session waits for its first frame without stale/static fallback", () =>
        {
            kb.Usb.Clear();
            SetMode(AuraMode.AMBIENT);
            typeof(PeripheralsProvider).GetMethod("InvalidateKeyboardFrames", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            Check(!kb.SyncFromLaptopAura(), "old capture cache invalidated");
            Idle(kb);
            Check(kb.Usb.Packets.Count == 0, "no invented static fallback before capture");
            Send(kb, []);
            Check(kb.Usb.Packets.Count == 0, "empty capture ignored");
            Send(kb, Zones);
            ValidateFrame(kb, "ClaymoreIIMainRight", 100);
        });

        Test("scalar software effects retain their original output", () =>
        {
            kb.Usb.Clear();
            SetMode(AuraMode.HEATMAP);
            PeripheralsProvider.StreamKeyboardColor(Color.Orange);
            Idle(kb);
            Check(kb.Usb.Packets.Count == 1, "one static packet for scalar effect");
            var p = kb.Usb.Packets[0];
            Check(p[1] == 0x51 && p[3] == 0 && p[10] == Color.Orange.R && p[11] == Color.Orange.G && p[12] == Color.Orange.B, "scalar color unchanged");
        });

        Test("queued native sync respects disabled sync", () =>
        {
            kb.Usb.Clear();
            SetMode(AuraMode.AuraBreathe);
            PeripheralsProvider.SetKeyboardAuraSync(false);
            Check(!kb.SyncFromLaptopAura() && kb.Usb.Packets.Count == 0, "no stale native write after disabling");
        });
        PeripheralsProvider.ConnectedKeyboards.Clear();
    }

    static void ValidateClaymoreIIWireMap(FakeClaymore kb, bool left)
    {
        var fixture = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "fixtures", "claymore-ii-main-ansi.csv"))
            .Skip(1).Select(line => line.Trim().Trim('"').Split("\",\""))
            .ToDictionary(parts => parts[0], parts => Convert.ToByte(parts[left ? 2 : 1], 16));
        var names = kb.KeyLayout().SelectMany(row => row).Select(key => key.Name).ToArray();
        var packets = Decode(kb.Usb.Packets);
        Check(names.Length == 87 && packets.Count == 87, "all ANSI main keys included");
        for (int i = 0; i < names.Length; i++)
            Check(packets[i].Id == fixture[names[i]], $"{names[i]} address: expected {fixture[names[i]]:X2}, got {packets[i].Id:X2}");
    }

    static void ValidateFrame(FakeClaymore kb, string layoutName, int brightness)
    {
        var layout = AuraKeyboardLayouts.Data[layoutName];
        var keys = Decode(kb.Usb.Packets);
        Check(keys.Select(x => x.Id).SequenceEqual(layout.LedIds), "exact existing main-key LED map; no guessed media/numpad IDs");
        var positions = new List<double>();
        foreach (var row in layout.Rows)
        {
            double x = 0;
            foreach (var key in row) { x += key.Gap; positions.Add(x + key.Width / 2); x += key.Width; }
        }
        double min = positions.Min(), width = positions.Max() - min;
        Check(keys.Count == positions.Count, "all mapped main keys covered");
        for (int i = 0; i < keys.Count; i++)
        {
            double p = (positions[i] - min) / width * 3;
            int a = Math.Min(3, (int)p), b = Math.Min(3, a + 1);
            int Channel(byte x, byte y) => (int)Math.Round(x + (y - x) * (p - a)) * brightness / 100;
            var expected = Color.FromArgb(Channel(Zones[a].R, Zones[b].R), Channel(Zones[a].G, Zones[b].G), Channel(Zones[a].B, Zones[b].B));
            Check(Math.Abs(keys[i].Color.R - expected.R) <= 1 && Math.Abs(keys[i].Color.G - expected.G) <= 1 && Math.Abs(keys[i].Color.B - expected.B) <= 1, "physical key-center interpolation and brightness");
        }
    }

    static List<(byte Id, Color Color)> Decode(IEnumerable<byte[]> packets)
    {
        var result = new List<(byte, Color)>();
        foreach (var p in packets)
        {
            Check(p.Length == 65 && p[0] == 0 && p[1] == 0xC0 && p[2] == 0x81 && p[3] is > 0 and <= 14 && p[4] == 0, "valid C0/81 packet; no static/save commands");
            for (int i = 0; i < p[3]; i++) result.Add((p[5 + 4 * i], Color.FromArgb(p[6 + 4 * i], p[7 + 4 * i], p[8 + 4 * i])));
        }
        Check(result.Count > 0, "frame present");
        return result;
    }
    static void Send(AsusKeyboard kb, Color[] colors) { PeripheralsProvider.StreamKeyboardColors(colors); Idle(kb); }
    static void SetMode(AuraMode mode) => AppConfig.Set("aura_mode", (int)mode);
    static void Idle(AsusKeyboard kb)
    {
        Check(SpinWait.SpinUntil(() => { lock (FrameLock.GetValue(kb)!) return !(bool)Worker.GetValue(kb)!; }, 5000), "worker drains within timeout");
    }
    static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + passed + ": " + name); }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}

sealed class FakeClaymore : ClaymoreIIWired
{
    public FakeUsb Usb { get; } = new();
    public FakeClaymore() { _usbProvider = Usb; IsDeviceReady = true; }
    public void Ready(bool value) => IsDeviceReady = value;
    public override bool IsDeviceConnected() => true;
    public override void SetProvider() => throw new InvalidOperationException("Hardware access forbidden in tests");
}
sealed class FakeUnsupported : AsusKeyboard
{
    public FakeUsb Usb { get; } = new();
    public FakeUnsupported() : base(0, 0) { _usbProvider = Usb; IsDeviceReady = true; }
    public override string GetDisplayName() => "Memory-only unsupported keyboard";
    public override bool IsDeviceConnected() => true;
    public override void SetProvider() => throw new InvalidOperationException("Hardware access forbidden in tests");
}
sealed class FakeUsb : UsbProvider
{
    public List<byte[]> Packets { get; } = new();
    public int Reads;
    public bool ThrowNextRead, RejectNextRead;
    public ManualResetEventSlim Entered { get; } = new(false);
    public ManualResetEventSlim Release { get; } = new(false);
    int block;
    public FakeUsb() : base(0, 0) { }
    public void Clear() { Packets.Clear(); Reads = 0; }
    public void BlockNextWrite() { Entered.Reset(); Release.Reset(); Interlocked.Exchange(ref block, 1); }
    public override void Write(byte[] data)
    {
        Packets.Add((byte[])data.Clone());
        if (Interlocked.Exchange(ref block, 0) == 1)
        {
            Entered.Set();
            if (!Release.Wait(5000)) throw new TimeoutException("test write gate");
        }
    }
    public override void Read(byte[] data)
    {
        Reads++;
        if (ThrowNextRead) { ThrowNextRead = false; throw new TimeoutException("simulated USB timeout"); }
        if (RejectNextRead) { RejectNextRead = false; data[1] = 0xFF; data[2] = 0xAA; return; }
        Array.Copy(Packets[^1], data, data.Length);
    }
    public override void Set(byte[] data) => throw new InvalidOperationException("Unexpected feature write");
    public override byte[] Get(byte[] data) => throw new InvalidOperationException("Unexpected feature read");
    public override void Dispose() { }
}
