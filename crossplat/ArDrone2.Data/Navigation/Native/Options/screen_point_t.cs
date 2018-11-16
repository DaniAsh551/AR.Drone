using System.Runtime.InteropServices;

namespace ArDrone2.Data.Navigation.Native.Options
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct screen_point_t
    {
        public int x;
        public int y;
    }
}