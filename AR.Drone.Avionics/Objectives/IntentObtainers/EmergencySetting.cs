﻿using System;

using AR.Drone.Avionics.Tools;
using AR.Drone.Avionics.Apparatus;

namespace AR.Drone.Avionics.Objectives.IntentObtainers
{
    public class EmergencySetting : IntentObtainer
    {
        public EmergencySetting(bool aCanBeOntained = false) : base(0.0f, Intent.DefaultAgression, aCanBeOntained) { /* Do Nothing */ }

        public override void Contribute(Apparatus.Output aApparatusOutput, ref Apparatus.Input aApparatusInput)
        {
            aApparatusInput.Command = Apparatus.Input.Type.Emergency;
            Obtained = true;
        }
    }
}
