namespace Hideez.Integration.Lite.Enums
{
    /// <summary>
    /// This is a copy of Hideez.SDK.Communication.ButtonPressCode enum
    /// You can use either one when parsing values received from Hideez Service
    /// </summary>
    public enum ButtonPressCode
    {
        None = 0x00,
        Single = 0x01,
        Double = 0x02,
        Triple = 0x03,
        Quad = 0x04,
        Penta = 0x05,
        Hexa = 0x06,
        Hepta = 0x07,
        Octa = 0x08,
        Multi = 0x09,
        Long = 0x10,
        SuperLong = 0x11
    }
}
