using System.Runtime.InteropServices;

namespace ArDrone2.Data.Navigation.Native.Math
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct vector21_t
    {
        public float x;
        public float y;
    }
}