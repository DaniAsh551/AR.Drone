using ArDrone2.Avionics.Apparatus;

namespace ArDrone2.Avionics.Objectives.IntentObtainers
{
    public interface IObtainer
    {
        void Contribute(Output aApparatusOutput, ref Input aApparatusInput);
    }
}