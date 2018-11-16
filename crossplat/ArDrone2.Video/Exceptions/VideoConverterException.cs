using System;

namespace ArDrone2.Video.Exceptions
{
    public class VideoConverterException : Exception
    {
        public VideoConverterException(string message)
            : base(message)
        {
        }
    }
}