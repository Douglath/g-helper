using GHelper.USB;

namespace GHelper.Peripherals.Keyboard.Models
{
    public class ClaymoreII : AsusKeyboard
    {
        public ClaymoreII() : this(0x196B)
        {
        }

        protected ClaymoreII(ushort productId) : base(0x0B05, productId)
        {
        }

        public override string GetDisplayName()
        {
            return "ROG Claymore II";
        }

        public override bool HasBattery()
        {
            return true;
        }

        public override void SynchronizeDevice()
        {
            ReadBattery();
            SetDeviceReady(IsDeviceConnected());
        }

        public override int MediaKeyCount()
        {
            return 4;
        }

        // Only the main keyboard has a spatial layout here, not the numpad/media keys.
        protected override bool SupportsSpatialAura => true;

        // Shared by the wired/wireless identities of the same keyboard. Explicit selection
        // avoids guessing undocumented fields in the profile/status response.
        public bool NumpadOnLeft
        {
            get => AppConfig.Is("claymore_ii_numpad_left");
            set
            {
                lock (this) AppConfig.Set("claymore_ii_numpad_left", value ? 1 : 0);
            }
        }

        protected override string? LayoutName()
        {
            return "ClaymoreIIMain" + (NumpadOnLeft ? "Left" : "Right") + (IsIsoLayout ? "ISO" : "");
        }

        protected override byte AuraSpeedByte(AuraSpeed speed)
        {
            return speed == AuraSpeed.Slow ? (byte)12 : speed == AuraSpeed.Fast ? (byte)4 : (byte)8;
        }
    }

    public class ClaymoreIIWired : ClaymoreII
    {
        public ClaymoreIIWired() : base(0x1934)
        {
        }

        public override string GetDisplayName()
        {
            return "ROG Claymore II (Wired)";
        }
    }
}
