using ArDrone2.Client;
using System;

namespace ArDrone2.MissionPlan
{
    /// <summary>
    /// Defines a Mission task that the Drone must do.
    /// </summary>
    public class Mission
    {
        /// <summary>
        /// Create a new Mission for the drone.
        /// </summary>
        /// <param name="droneClient">The DroneClient controlling the drone in question.</param>
        /// <param name="missionAction">The action to carry out in the Mission.</param>
        /// <param name="onMissionStatusChange">The DroneStatus change event handler.</param>
        public Mission(DroneClient droneClient, Action<DroneClient> missionAction, Action<DroneClient, MissionEventArguments> onMissionStatusChange = null)
        {
            _droneClient = droneClient;
            _missionAction = missionAction;
            if(onMissionStatusChange != null)
            _onMissionStatusChange = onMissionStatusChange;
            _missionStatus = MissionStatus.WaitingEnque;
        }


        private readonly Action<DroneClient> _missionAction;
        private readonly Action<DroneClient, MissionEventArguments> _onMissionStatusChange;
        private readonly DroneClient _droneClient;
        private MissionStatus _missionStatus { get; set; }

        /// <summary>
        /// The Status of the Mission.
        /// </summary>
        public MissionStatus MissionStatus { get => _missionStatus; private set
            {
                _missionStatus = value;
                var missionEventArgs = new MissionEventArguments(_droneClient, this);
                OnMissionStatusChange(missionEventArgs);
            } }
        
        public event Action<DroneClient, MissionEventArguments> MissionStatusChange;

        /// <summary>
        /// Sets the MissionStatus to Aborted.
        /// </summary>
        internal void AbortMission()
        {
            MissionStatus = MissionStatus.Aborted;
        }
        
        private void OnMissionStatusChange(MissionEventArguments missionEventArguments)
        {
            if (MissionStatusChange == null)
                return;

            MissionStatusChange(missionEventArguments.DroneClient, missionEventArguments);
        }

        /// <summary>
        /// Instructs the Mission to execute. Sets the MissionStatus accordingly as it progresses.
        /// </summary>
        public void DoMission()
        {
            MissionStatus = MissionStatus.InProgress;
            _missionAction(_droneClient);
            MissionStatus = MissionStatus.Completed;
        }
    }
}
