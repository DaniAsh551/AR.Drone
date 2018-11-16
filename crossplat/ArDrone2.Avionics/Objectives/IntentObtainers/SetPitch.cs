using ArDrone2.Avionics.Apparatus;
using ArDrone2.Avionics.Tools;

namespace ArDrone2.Avionics.Objectives.IntentObtainers
{
    public class SetPitch : IntentObtainer
    {
        public SetPitch(float aValue) : base(aValue, DefaultAgression)
        {
            /* Do Nothing */
        }

        public override void Contribute(Output aApparatusOutput, ref Input aApparatusInput)
        {
            aApparatusInput.Pitch = Arithmetics.KeepInRange(Value, Input.Limits.Pitch.Min, Input.Limits.Pitch.Max);
        }
    }
}