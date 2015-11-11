﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScienceAlert.Experiments.Sensors
{
    [Flags]
    public enum SensorState
    {
        NoAlert = 0,                                            
        RecoveryAlert = 1 << 0,                                   
        TransmitAlert = 1 << 1,                                 

        Both = RecoveryAlert | TransmitAlert,
        RecoveryAndTransmitAlert = Both
    }


    public interface IExperimentSensor
    {
        float RecoveryValue { get; }
        float TransmissionValue { get; }
        string Subject { get; }
        ProfileData.SensorSettings Settings { get; }
        SensorState Status { get; }
        ScienceExperiment Experiment { get; }

        void UpdateState(CelestialBody body, ExperimentSituations situation);
        bool DeployThisExperiment();
        void SetScienceModules(ModuleScienceExperiment moduleList);
    }
}