using System.Runtime.InteropServices;

namespace ArDrone2.Data.Navigation.Native.Options
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct velocities_t
    {
        public float x;
        public float y;
        public float z;
    }
}