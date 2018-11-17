using System;
using System.Collections.Generic;
using System.Text;

namespace ArDrone2.MissionPlan
{
    /// <summary>
    /// Defines the statuses for Missions.
    /// </summary>
    public enum MissionStatus
    {
        /// <summary>
        /// Mission is waiting to be enqued.
        /// </summary>
        WaitingEnque = -2,
        /// <summary>
        /// Mission has been aborted.
        /// </summary>
        Aborted = -1,
        /// <summary>
        /// Mission is currently in que, awaiting activation.
        /// </summary>
        Enqued = 0,
        /// <summary>
        /// Mission has been activated and is in progress.
        /// </summary>
        InProgress = 1,
        /// <summary>
        /// The mission has been completed, either successfully or unsuccessfully.
        /// </summary>
        Completed = 2,
    }
}
