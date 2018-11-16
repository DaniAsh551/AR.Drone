using ArDrone2.Avionics.Tools.Time;

namespace ArDrone2.Avionics.Objectives
{
    /// <summary>
    /// Objective that flat trims the drone
    /// </summary>
    public class EmergencyReset : Objective
    {
        private void CreateTask()
        {
            Add(new IntentObtainers.EmergencyResetting(true));
        }

        public EmergencyReset(long aDuration = 0) : base(aDuration)
        {
            CreateTask();
        }

        public EmergencyReset(Expiration aExpiration) : base(aExpiration)
        {
            CreateTask();
        }
    }
}