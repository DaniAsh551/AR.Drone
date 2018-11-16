using ArDrone2.Avionics.Apparatus;

namespace ArDrone2.Avionics.Objectives.IntentObtainers
{
    public class EmergencySetting : IntentObtainer
    {
        public EmergencySetting(bool aCanBeOntained = false) : base(0.0f, DefaultAgression, aCanBeOntained)
        {
            /* Do Nothing */
        }

        public override void Contribute(Output aApparatusOutput, ref Input aApparatusInput)
        {
            aApparatusInput.Command = Input.Type.Emergency;
            Obtained = true;
        }
    }
}