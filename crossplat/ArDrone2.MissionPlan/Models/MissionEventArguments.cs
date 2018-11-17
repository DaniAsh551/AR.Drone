using ArDrone2.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArDrone2.MissionPlan
{
    /// <summary>
    /// The arguments passed as to the MissionStatus Change Event handler.
    /// </summary>
    public class MissionEventArguments
    {
        /// <summary>
        /// Creates a new set of MissionEventArguments.
        /// </summary>
        /// <param name="droneClient">The DroneClient to which the mission belongs to.</param>
        /// <param name="mission">The mission in question.</param>
        public MissionEventArguments(DroneClient droneClient, Mission mission)
        {
            Timestamp = DateTime.Now;
            DroneClient = droneClient;
            Mission = mission;
        }

        /// <summary>
        /// The DroneClient to which the mission belongs to.
        /// </summary>
        public readonly DroneClient DroneClient;
        /// <summary>
        /// The time at which this status was changed.
        /// </summary>
        public readonly DateTime Timestamp;
        /// <summary>
        /// The mission to whom this event belongs to.
        /// </summary>
        public readonly Mission Mission;
        /// <summary>
        /// The Status of the mission in question.
        /// </summary>
        public MissionStatus MissionStatus => Mission.MissionStatus;
    }
}
