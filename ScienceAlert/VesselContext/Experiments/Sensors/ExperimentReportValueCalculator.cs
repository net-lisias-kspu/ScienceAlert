﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReeperCommon.Logging;
using ScienceAlert.Game;
using UnityEngine;
using ScienceModuleList = System.Collections.Generic.List<ScienceAlert.Game.IModuleScienceExperiment>;


namespace ScienceAlert.VesselContext.Experiments.Sensors
{
    public class ExperimentReportValueCalculator : IExperimentReportValueCalculator
    {
        private readonly ReadOnlyCollection<ScienceExperiment> _experiments;
        private readonly float _careerMultiplier;
        private readonly ICelestialBody _homeWorld;
        private readonly IQueryScienceValue _scienceValueQuery;
        private readonly IVessel _activeVessel;

        private Dictionary<ScienceExperiment, ScienceModuleList> _experimentModules =
            new Dictionary<ScienceExperiment, ScienceModuleList>();

        public ExperimentReportValueCalculator(
            ReadOnlyCollection<ScienceExperiment> experiments,
            [Name(CrossContextKeys.CareerScienceGainMultiplier)] float careerMultiplier,
            [Name(CrossContextKeys.HomeWorld)] ICelestialBody homeWorld,
            IQueryScienceValue scienceValueQuery,
            IVessel activeVessel)
        {
            if (experiments == null) throw new ArgumentNullException("experiments");
            if (homeWorld == null) throw new ArgumentNullException("homeWorld");
            if (scienceValueQuery == null) throw new ArgumentNullException("scienceValueQuery");
            if (activeVessel == null) throw new ArgumentNullException("activeVessel");

            _experiments = experiments;
            _careerMultiplier = careerMultiplier;
            _homeWorld = homeWorld;
            _scienceValueQuery = scienceValueQuery;
            _activeVessel = activeVessel;
        }


        public void OnActiveVesselModified()
        {
            UpdateExperimentModuleDictionary();
        }


        public float CalculateRecoveryValue(ScienceExperiment experiment, IScienceSubject subject)
        {
            if (experiment == null) throw new ArgumentNullException("experiment");
            if (subject == null) throw new ArgumentNullException("subject");

            return CalculateReportValue(experiment, subject, 1f);
        }


        public float CalculateTransmissionValue(ScienceExperiment experiment, IScienceSubject subject)
        {
            return CalculateReportValue(experiment, subject, GetBestTransmissionMultiplier(experiment));
        }


        public float CalculateLabValue(ScienceExperiment experiment, IScienceSubject subject)
        {
            var labs = _activeVessel.Labs;

            // if there aren't any labs, or aren't any labs which haven't processed a subject by this id, then no
            // lab data is possible
            if (labs.Count == 0) return 0f;

            IScienceLab lab;

            if ((lab = labs.FirstOrDefault(l => !l.HasAnalyzedSubject(subject))) == null) return 0f;

            return Mathf.Round(GetScienceLabMultiplier(lab, subject) *
                                          _scienceValueQuery.GetReferenceDataValue(experiment.baseValue * experiment.dataScale,
                                              subject) * _careerMultiplier);
        }


        private float CalculateReportValue(ScienceExperiment experiment, IScienceSubject subject, float xmitScalar)
        {
            var dataAmount = experiment.baseValue * experiment.dataScale;
            var onboardReports = GetOnboardReportsMatching(subject);

            if (onboardReports == 0)
                return _scienceValueQuery.GetScienceValue(dataAmount, subject, xmitScalar) * _careerMultiplier;

            var experimentValue = _scienceValueQuery.GetNextScienceValue(dataAmount, subject, xmitScalar) * _careerMultiplier;

            if (onboardReports == 1)
                return experimentValue;

            return experimentValue / Mathf.Pow(4f, onboardReports - 1);
        }


        private void UpdateExperimentModuleDictionary()
        {
            _experimentModules = _experiments
                .ToDictionary(exp => exp, exp => _activeVessel.ScienceExperimentModules
                    .Where(mse => mse.ExperimentID == exp.id)
                    .OrderByDescending(mse => mse.TransmissionMultiplier)
                    .ToList());
        }


        private static int CountReportsMatchingSubjectId(ScienceData[] data, string subjectId)
        {
            int counter = 0;

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var d in data)
                if (d.subjectID == subjectId) ++counter;

            return counter;
        }


        private int GetOnboardReportsMatching(IScienceSubject subject)
        {
            int matches = 0;
            var containers = _activeVessel.Containers;

            // this is a performance-critical and often-used method so avoid creating ANY garbage
            for (int idx = 0; idx < containers.Count; ++idx)
            {
                var container = containers[idx];
                if (container.GetScienceCount() > 0)
                    matches += CountReportsMatchingSubjectId(container.GetData(), subject.Id);
            }

            return matches;
        }


        // todo: only available ones?
        private float GetBestTransmissionMultiplier(ScienceExperiment experiment)
        {
            try
            {
                var modulesForExperiment = _experimentModules[experiment];

                return modulesForExperiment.Any() ? modulesForExperiment.First().TransmissionMultiplier : 0f;
            }
            catch (KeyNotFoundException knf)
            {
                // something is really hosed here
                foreach (var exp in _experimentModules)
                    Log.Error("Known experiment type: " + exp.Key.id);

                throw new MissingExperimentException(experiment, knf);
            }
        }


        private float GetScienceLabMultiplier(IScienceLab lab, IScienceSubject subject)
        {
            var multiplier = 1f;

            // lab can't process data twice
            if (lab.HasAnalyzedSubject(subject)) return 0f;

            if (_activeVessel.Landed)
                multiplier *= 1f + lab.SurfaceBonus;

            if (subject.Id.Contains(_activeVessel.OrbitingBody.BodyName)) // yes, a simple Contains is to replicate stock behaviour
                multiplier *= 1f + lab.ContextBonus;

            if ((_activeVessel.Landed || _activeVessel.SplashedDown) &&
                _homeWorld.Equals(_activeVessel.OrbitingBody))
                multiplier *= lab.HomeworldMultiplier; // lack of addition intended

            return multiplier;
        }
    }
}