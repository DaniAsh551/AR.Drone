using System;

namespace ArDrone2.Video.Exceptions
{
    public class VideoDecoderException : Exception
    {
        public VideoDecoderException(string message) : base(message)
        {
        }
    }
}