﻿using ArDrone2.Data.Navigation;

namespace ArDrone2.Avionics.Apparatus
{
    /// <summary>
    /// Contains an Ar.Drone apparatus navigation data output (the data the comes from the drone)
    /// </summary>
    public class Output
    {
        /// <summary>
        /// Navigation info retreived from the drone
        /// </summary>
        public NavigationData Navigation;

        /// <summary>
        /// Last input that had been sent to a drone
        /// </summary>
        public Input LastInput;
    }
}
