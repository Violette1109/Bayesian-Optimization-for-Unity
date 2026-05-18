using System;
using System.Collections.Generic;
using UnityEngine;

namespace BOforUnity
{
    [Serializable]
    public class ExperimentScaleOption
    {
        public string label = "5-point";
        [Min(2)] public int maxValue = 5;
    }

    [Serializable]
    public class ExperimentRoundPreset
    {
        public string label = "10 Sampling + 5 Optimization";
        [Min(0)] public int samplingRounds = 10;
        [Min(0)] public int optimizationRounds = 5;
    }

    [CreateAssetMenu(
        fileName = "ExperimentRuntimeConfig",
        menuName = "BOforUnity/Experiment Runtime Config",
        order = 1)]
    public class ExperimentRuntimeConfig : ScriptableObject
    {
        public List<ExperimentScaleOption> scaleOptions = new List<ExperimentScaleOption>
        {
            new ExperimentScaleOption { label = "5-point", maxValue = 5 },
            new ExperimentScaleOption { label = "20-point", maxValue = 20 },
            new ExperimentScaleOption { label = "100-point", maxValue = 100 }
        };

        public int defaultScaleIndex = 0;

        public List<ExperimentRoundPreset> roundPresets = new List<ExperimentRoundPreset>
        {
            new ExperimentRoundPreset { label = "10 Sampling + 5 Optimization", samplingRounds = 10, optimizationRounds = 5 },
            new ExperimentRoundPreset { label = "15 Sampling + 0 Optimization", samplingRounds = 15, optimizationRounds = 0 }
        };

        public int defaultRoundPresetIndex = 0;
        public string feedbackObjectiveKey = "mental_demand";
        public string targetSliderName = "SliderBar";
    }
}
