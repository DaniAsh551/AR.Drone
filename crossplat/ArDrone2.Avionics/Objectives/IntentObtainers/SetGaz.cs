using ArDrone2.Avionics.Apparatus;
using ArDrone2.Avionics.Tools;

namespace ArDrone2.Avionics.Objectives.IntentObtainers
{
    public class SetGaz : IntentObtainer
    {
        public SetGaz(float aValue) : base(aValue, DefaultAgression)
        {
            /* Do Nothing */
        }

        public override void Contribute(Output aApparatusOutput, ref Input aApparatusInput)
        {
            aApparatusInput.Gaz = Arithmetics.KeepInRange(Value, Input.Limits.Gaz.Min, Input.Limits.Gaz.Max);
        }
    }
}