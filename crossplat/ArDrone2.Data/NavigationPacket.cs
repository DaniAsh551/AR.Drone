using System.Runtime.InteropServices;

namespace ArDrone2.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NavigationPacket
    {
        public long Timestamp;
        public byte[] Data;
    }
}