using ArDrone2.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArDrone2.MissionPlan
{
    /// <summary>
    /// Defines a set of instructions which should be followed in sequential order by the drone.
    /// </summary>
    public class MissionPlan
    {
        private Task _missionTask { get; set; }
        private CancellationTokenSource _cancellationTokenSource { get; set; }
        private Queue<Mission> _missions { get; set; }
        /// <summary>
        /// The current mission which is being executed.
        /// </summary>
        public Mission CurrentMission { get; private set; }

        /// <summary>
        /// The action to handle the MissionPlan Start event.
        /// </summary>
        public event Action<MissionPlan> MissionPlanStarted;
        /// <summary>
        /// The action to handle the MissionPlan finish event.
        /// </summary>
        public event Action<MissionPlan> MissionPlanFinished;


        /// <summary>
        /// Enques a mission in this MissionPlan.
        /// </summary>
        /// <param name="mission">The mission to enque.</param>
        /// <param name="onMissionStatusChange">The mission status change event handler.</param>
        public void EnqueMission(Mission mission, Action<DroneClient, MissionEventArguments> onMissionStatusChange = null)
        {
            mission.MissionStatusChange += onMissionStatusChange;
            _missions.Enqueue(mission);
        }

        /// <summary>
        /// Instructs the mission plan to pause the MissionPlan until a certain action has been completed.
        /// </summary>
        /// <param name="action">The action to wait for before resuming the MissionPlan.</param>
        /// <param name="blockUntilDone">Whether the Thread execution should be blocked until the MissionPlan resumes execution.</param>
        public void PauseMissionsUntil(Action action, bool blockUntilDone = true)
        {
            _cancellationTokenSource.Cancel();
            CurrentMission.AbortMission();

            if (blockUntilDone)
            {
                action();
                _cancellationTokenSource = new CancellationTokenSource();
                _missionTask = Task.Factory.StartNew(() => {
                    CurrentMission.DoMission();
                    ExecuteNextMission();
                });
            }
            else
                Task.Factory.StartNew(() => 
                {
                    action();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _missionTask = Task.Factory.StartNew(() => {
                        CurrentMission.DoMission();
                        ExecuteNextMission();
                    });
                });
        }

        /// <summary>
        /// Instructs the MissionPlan to execute the next mission. This is followed only if there were no previous missions or if the prevous misison was aborted or completed.
        /// </summary>
        private void ExecuteNextMission()
        {
            if (CurrentMission != null && !(new MissionStatus[] { MissionStatus.Completed, MissionStatus.Aborted }.Contains(CurrentMission.MissionStatus)))
                return;
            CurrentMission = _missions.Dequeue();
            if (CurrentMission != null)
                ExecuteMission();
            else if (MissionPlanFinished != null)
                MissionPlanFinished(this);

        }
        
        private void ExecuteMission()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _missionTask = Task.Factory.StartNew(() => {
                CurrentMission.DoMission();
                ExecuteNextMission();
            });
        }


        public void Execute()
        {
            if (CurrentMission != null)
                return;

            ExecuteNextMission();
            if (MissionPlanStarted != null)
                MissionPlanStarted(this);
        }
    }
}
