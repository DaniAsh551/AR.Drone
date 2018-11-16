using ArDrone2.Avionics.Apparatus;

namespace ArDrone2.Avionics.Objectives.IntentObtainers
{
    public class Hovering : IntentObtainer
    {
        public Hovering() : base(0.0f)
        {
            /* Do Nothing */
        }

        public override void Contribute(Output aApparatusOutput, ref Input aApparatusInput)
        {
            aApparatusInput.Command = Input.Type.Hover;
        }
    }
}