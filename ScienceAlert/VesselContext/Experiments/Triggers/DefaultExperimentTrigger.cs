﻿using System;
using System.Collections;
using System.Linq;
using JetBrains.Annotations;
using ReeperCommon.Containers;
using ReeperCommon.Utilities;
using strange.extensions.promise.api;
using strange.extensions.promise.impl;
using ScienceAlert.Game;

namespace ScienceAlert.VesselContext.Experiments.Triggers
{
    public class DefaultExperimentTrigger : IExperimentTrigger
    {
        protected readonly ScienceExperiment Experiment;
        protected readonly IVessel ActiveVessel;
        protected readonly IScienceUtil ScienceUtil;

        protected Maybe<IPromise> UnfulfilledPromise = Maybe<IPromise>.None;




        public DefaultExperimentTrigger([NotNull] ScienceExperiment experiment, [NotNull] IVessel activeVessel,
            [NotNull] IScienceUtil scienceUtil)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (activeVessel == null) throw new ArgumentNullException("activeVessel");
            if (scienceUtil == null) throw new ArgumentNullException("scienceUtil");
            Experiment = experiment;
            ActiveVessel = activeVessel;
            ScienceUtil = scienceUtil;
        }

        public virtual IPromise Deploy()
        {
            var promise = new Promise();

            if (IsBusy)
            {
                promise.ReportFail(new TriggerIsBusyException(Experiment));
                return promise;
            }

            UnfulfilledPromise = Maybe<IPromise>.With(promise);

            var module = GetSuitableModule();

            if (!module.HasValue)
            {
                promise.ReportFail(new NoAvailableScienceModuleException(Experiment));
            } else CoroutineHoster.Instance.StartCoroutine(DeployExperiment(module.Value));

            return promise;
        }


        private Maybe<IModuleScienceExperiment> GetSuitableModule()
        {
            // todo: more intelligent selection criteria? xmit scalar etc
            return ActiveVessel.ScienceExperimentModules
                .Where(mse => mse.ExperimentID == Experiment.id)
                .Where(mse => mse.CanBeDeployed)
                .FirstOrDefault(mse => ScienceUtil.RequiredUsageInternalAvailable(ActiveVessel, mse.Part,
                    mse.InternalUsageRequirements))
                .ToMaybe();
        }


        public virtual bool IsBusy
        {
            get { return UnfulfilledPromise.HasValue && UnfulfilledPromise.Value.State == BasePromise.PromiseState.Pending; }
        }


        private IEnumerator DeployExperiment(IModuleScienceExperiment module)
        {
            if (module == null || !module.CanBeDeployed)
            {
                UnfulfilledPromise.Do(
                    p => p.ReportFail(new ArgumentException("specified module cannot be deployed", "module")));
                UnfulfilledPromise = Maybe<IPromise>.None;
                yield break;
            }
            module.Deploy();
            UnfulfilledPromise.Do(p => p.Dispatch());
            UnfulfilledPromise = Maybe<IPromise>.None;
        }
    }
}


//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using JetBrains.Annotations;
//using ReeperCommon.Containers;
//using ReeperCommon.Logging;
//using ReeperCommon.Utilities;
//using strange.extensions.promise.api;
//using strange.extensions.promise.impl;
//using ScienceAlert.Game;
//using UnityEngine;


//namespace ScienceAlert.VesselContext.Experiments.Sensors.Trigger
//{
//    // ReSharper disable once UnusedMember.Global
//    class DefaultDeploymentTrigger : ExperimentTrigger
//    {
//        private const float AnimationTimeout = 5.0f; // max time to spend waiting for callbacks

//        private readonly IScienceUtil _scienceUtil;

//        private readonly List<KeyValuePair<IScalarModule, EventData<float>.OnEvent>> _expectedCallbacks =
//            new List<KeyValuePair<IScalarModule, EventData<float>.OnEvent>>();

//        public DefaultDeploymentTrigger(IVessel activeVessel, ScienceExperiment experiment, [NotNull] IScienceUtil scienceUtil) : base(activeVessel, experiment)
//        {
//            if (scienceUtil == null) throw new ArgumentNullException("scienceUtil");
//            _scienceUtil = scienceUtil;
//        }


//        public override IPromise Deploy()
//        {
//            var promise = new Promise();

//            try
//            {
//                CoroutineHoster.Instance.StartCoroutine(Deploy(GetSuitableModule(), promise));
//            }
//            catch (Exception e)
//            {
//                promise.ReportFail(e);
//            }

//            return promise;
//        }


//        protected virtual IModuleScienceExperiment GetSuitableModule()
//        {
//            return ActiveVessel.ScienceExperimentModules
//                .Where(mse => mse.ExperimentID == Experiment.id)
//                .Where(mse => mse.CanBeDeployed)
//                .FirstOrDefault(mse => _scienceUtil.RequiredUsageInternalAvailable(ActiveVessel, mse.Part, mse.InternalUsageRequirements))
//                .IfNull(() => { throw new NoSuitableScienceExperimentModuleFoundException(Experiment); });
//        }


//        private IEnumerator Deploy(IModuleScienceExperiment selectedModule, IPromise promise)
//        {
//            selectedModule.Deploy();

//            if (selectedModule.FxIndices.Any())
//            {
//                CreateAnimationCallbacks(selectedModule);

//                if (_expectedCallbacks.Any())
//                {
//                    Log.Debug("Waiting on " + _expectedCallbacks.Count +
//                              " animation callbacks before deployment is complete");
//                    yield return CoroutineHoster.Instance.StartCoroutine(WaitForAnimationCallbacksToFinish(promise));
//                    Log.Debug("Finished waiting on animation callbacks");
//                }
//                else
//                    Log.Verbose(selectedModule.ModuleTypeName + " for " + selectedModule.ExperimentID +
//                                " appears to have animation Fx but no animation callbacks were created");
//            }

//            promise.Dispatch(); 
//        }


//        private void CreateAnimationCallbacks(IModuleScienceExperiment mse)
//        {
//            Log.Debug("Creating animation callbacks for " + mse.ModuleTypeName);

//            var modulesOnPart = mse.Part.Modules;

//            var modulesThatSendAnimationCallbacks = mse.FxIndices.Value
//                .Select(moduleIdx => modulesOnPart[moduleIdx])
//                .Where(pm => pm != null)
//                .OfType<IScalarModule>()
//                .ToList();

//            foreach (var module in modulesThatSendAnimationCallbacks)
//                InsertCallback(module);
//        }


//        private void InsertCallback(IScalarModule fromModule)
//        {
//            EventData<float>.OnEvent callback = null;

//            // this callback will just remove itself from the expected callback list when invoked
//            callback = f =>
//            {
//                ClearCallback(fromModule, callback);
//            };

//            fromModule.OnStop.Add(callback);
//        }


//        private IEnumerator WaitForAnimationCallbacksToFinish(IPromise promise)
//        {
//            var timeout = Time.realtimeSinceStartup + AnimationTimeout;

//            while (_expectedCallbacks.Any() || Time.realtimeSinceStartup < timeout)
//                yield return null;

//            if (Time.realtimeSinceStartup >= timeout && _expectedCallbacks.Any())
//            {
//                Log.Verbose("Timed out while waiting for animation callbacks for the following module(s):");
//                foreach (var cb in _expectedCallbacks)
//                    Log.Verbose("  Module: " + cb.GetType().Name);

//                ClearCallbacks();
//            }

//            promise.Dispatch();
//        }


//        private void ClearCallbacks()
//        {
//            while (_expectedCallbacks.Any())
//                ClearCallback(_expectedCallbacks.First().Key, _expectedCallbacks.First().Value);
//        }


//        private void ClearCallback(IScalarModule module, EventData<float>.OnEvent callback)
//        {
//            foreach (var kvp in _expectedCallbacks)
//                if (ReferenceEquals(kvp.Key, module))
//                {
//                    module.OnStop.Remove(callback);
//                    return;
//                }

//            Log.Warning("Tried to remove a callback from " + module.GetType().Name +
//                        " but it was not found in the stored callback list!");
//        }


//        public override bool Busy
//        {
//            get { return _expectedCallbacks.Any(); }
//        }
//    }
//}