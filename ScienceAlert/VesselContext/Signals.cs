﻿using strange.extensions.signal.impl;

namespace ScienceAlert.VesselContext
{

// ReSharper disable once ClassNeverInstantiated.Global
    class SignalExperimentSensorStatusChanged : Signal<ExperimentSensorState>
    {
    }


    class SignalDeployExperiment : Signal<ScienceExperiment>
    {
    }

    class SignalDeployExperimentFinished : Signal<ScienceExperiment, bool /* successfully? */>
    {
        
    }
}
