using GHelper.AnimeMatrix.Communication.Platform;
using HidSharp;

namespace GHelper.Peripherals.Keyboard;

public sealed class RogClaymoreII : IPeripheral, IDisposable
{
    private const ushort AsusVendorId = 0x0B05;
    private const int PacketSize = 65;
    private const int LedCount = 120;
    private const int LedsPerPacket = 15;

    public static readonly ushort[] SupportedProductIds = [0x1934, 0x196B];

    private static readonly byte[] KeysRight =
    [
        6, 14,
        0, 24, 32, 40, 48, 64, 72, 80, 88, 96, 104, 112, 120, 128, 136, 144,
        1, 17, 25, 33, 41, 49, 57, 65, 73, 81, 89, 97, 105, 121, 129, 137, 145, 153, 161, 169, 177,
        2, 18, 26, 34, 42, 50, 58, 66, 74, 82, 90, 98, 106, 122, 130, 138, 146, 154, 162, 170, 178,
        3, 19, 27, 35, 43, 51, 59, 67, 75, 83, 91, 99, 107, 123, 155, 163, 171,
        4, 12, 20, 28, 36, 44, 52, 60, 68, 76, 84, 92, 124, 140, 156, 164, 172, 180,
        5, 13, 21, 53, 77, 93, 101, 125, 133, 141, 149, 157, 173,
    ];

    private static readonly byte[] KeysLeft =
    [
        6, 14,
        32, 56, 64, 72, 80, 96, 104, 112, 120, 128, 136, 144, 152, 160, 168, 176,
        33, 49, 57, 65, 73, 81, 89, 97, 105, 113, 121, 129, 137, 153, 161, 169, 177, 1, 9, 17, 25,
        34, 50, 58, 66, 74, 82, 90, 98, 106, 114, 122, 130, 138, 154, 162, 170, 178, 2, 10, 18, 26,
        35, 51, 59, 67, 75, 83, 91, 99, 107, 115, 123, 131, 139, 155, 3, 11, 19,
        36, 44, 52, 60, 68, 76, 84, 92, 100, 108, 116, 124, 156, 172, 4, 12, 20, 28,
        37, 45, 53, 85, 109, 125, 133, 157, 165, 173, 181, 5, 21,
    ];

    private readonly ushort productId;
    private readonly string path;
    private readonly object ioLock = new();
    private WindowsUsbProvider? usb;
    private byte[] keys = KeysRight;
    private int streamingBusy;

    public RogClaymoreII(ushort productId, string path)
    {
        this.productId = productId;
        this.path = path;
    }

    public bool IsDeviceReady => usb is not null;
    public bool Wireless => productId == 0x196B;
    public int Battery => -1;
    public bool Charging => false;

    public static IEnumerable<RogClaymoreII> Detect()
    {
        foreach (ushort productId in SupportedProductIds)
        {
            foreach (HidDevice device in DeviceList.Local.GetHidDevices(AsusVendorId, productId))
            {
                if (!device.CanOpen || !device.DevicePath.Contains("mi_01", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return new RogClaymoreII(productId, device.DevicePath);
            }
        }
    }

    public bool IsConnected()
        => DeviceList.Local.GetHidDevices(AsusVendorId, productId)
            .Any(device => string.Equals(device.DevicePath, path, StringComparison.OrdinalIgnoreCase));

    public void Connect()
    {
        if (usb is not null) return;
        usb = new WindowsUsbProvider(AsusVendorId, productId, path, 100);
        ReadLayout();
        Logger.WriteLine($"{GetDisplayName()}: connected on {path}");
    }

    private void ReadLayout()
    {
        try
        {
            byte[] packet = new byte[PacketSize];
            packet[1] = 0x12;

            lock (ioLock)
            {
                usb?.Write(packet);
                byte[] response = new byte[PacketSize];
                usb?.Read(response);
                keys = response[17] is 0xFF or 0x01 ? KeysRight : KeysLeft;
            }
        }
        catch (Exception e)
        {
            keys = KeysRight;
            Logger.WriteLine($"{GetDisplayName()}: layout query failed, assuming right numpad: {e.Message}");
        }
    }

    public void WriteColors(IReadOnlyList<Color> colors)
    {
        if (!IsDeviceReady || colors.Count == 0) return;
        if (Interlocked.CompareExchange(ref streamingBusy, 1, 0) != 0) return;

        try
        {
            Color[] palette = colors.Count >= 4
                ? [colors[0], colors[1], colors[2], colors[3]]
                : [colors[0], colors[0], colors[0], colors[0]];

            byte[] ledData = Enumerable.Repeat((byte)0xFF, LedCount * 4).ToArray();
            int usableKeys = Math.Min(keys.Length, LedCount);

            for (int i = 0; i < usableKeys; i++)
            {
                int approximateColumn = keys[i] / 8;
                Color color = palette[Math.Min(3, approximateColumn * 4 / 23)];
                int offset = i * 4;
                ledData[offset] = keys[i];
                ledData[offset + 1] = color.R;
                ledData[offset + 2] = color.G;
                ledData[offset + 3] = color.B;
            }

            lock (ioLock)
            {
                for (int packetIndex = 0; packetIndex < LedCount / LedsPerPacket; packetIndex++)
                {
                    byte[] packet = new byte[PacketSize];
                    packet[1] = 0xC0;
                    packet[2] = 0x81;
                    packet[3] = (byte)(0x90 - LedsPerPacket * packetIndex);
                    Buffer.BlockCopy(ledData, packetIndex * LedsPerPacket * 4, packet, 5, LedsPerPacket * 4);
                    usb?.Write(packet);
                }
            }
        }
        catch (Exception e)
        {
            Logger.WriteLine($"{GetDisplayName()}: lighting write failed: {e.Message}");
            Dispose();
        }
        finally
        {
            Interlocked.Exchange(ref streamingBusy, 0);
        }
    }

    public bool CanExport() => false;
    public byte[] Export() => [];
    public bool Import(byte[] blob) => false;
    public PeripheralType DeviceType() => PeripheralType.Keyboard;
    public string GetDisplayName() => "ROG Claymore II";
    public bool HasBattery() => false;
    public void SynchronizeDevice() => Connect();
    public void ReadBattery() { }

    public override bool Equals(object? obj)
        => obj is RogClaymoreII other
           && productId == other.productId
           && string.Equals(path, other.path, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode()
        => HashCode.Combine(productId, path.ToUpperInvariant());

    public void Dispose()
    {
        usb?.Dispose();
        usb = null;
    }
}
