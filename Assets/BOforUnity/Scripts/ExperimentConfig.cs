using System;
using System.Collections.Generic;
using BOforUnity;
using UnityEngine;
using UnityEngine.UI;

public class ExperimentConfig : MonoBehaviour
{
    [Header("Data-Driven Runtime Config")]
    public ExperimentRuntimeConfig runtimeConfig;

    [Header("Config Panel UI")]
    public GameObject configPanel;
    public List<Button> scaleOptionButtons = new List<Button>();
    public List<Button> roundPresetButtons = new List<Button>();
    public Toggle warmStartToggle;
    public Button startBtn;

    [Header("References")]
    public Slider likertSlider;
    public BoForUnityManager boManager;

    private readonly Color _selectedColor = new Color(0.498f, 0.467f, 0.867f);
    private readonly Color _defaultColor = new Color(0.9f, 0.9f, 0.9f);

    private static int _selectedScaleIndex = -1;
    private static int _selectedRoundPresetIndex = -1;
    private static bool _warmStart = false;
    private static bool _experimentStarted = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _selectedScaleIndex = -1;
        _selectedRoundPresetIndex = -1;
        _warmStart = false;
        _experimentStarted = false;
    }

    void Awake()
    {
        ValidateSetupOrThrow();
        ClampSelectionIndices();

        if (!_experimentStarted)
        {
            configPanel.SetActive(true);
            boManager.welcomePanel.SetActive(false);
            boManager.nextButton.SetActive(false);

            if (boManager.pythonStarter != null)
                boManager.pythonStarter.enabled = false;
        }
        else
        {
            configPanel.SetActive(false);
            ApplyConfig();
        }
    }

    void Start()
    {
        if (_experimentStarted)
            return;

        BindScaleButtons();
        BindRoundButtons();

        warmStartToggle.onValueChanged.AddListener(val => _warmStart = val);
        startBtn.onClick.AddListener(OnStartClicked);

        _warmStart = warmStartToggle.isOn;
        HighlightScale(_selectedScaleIndex);
        HighlightRoundPreset(_selectedRoundPresetIndex);
    }

    private void ValidateSetupOrThrow()
    {
        if (runtimeConfig == null)
            throw new InvalidOperationException("ExperimentConfig requires a Runtime Config asset.");
        if (boManager == null)
            throw new InvalidOperationException("ExperimentConfig requires a BoForUnityManager reference.");
        if (configPanel == null || warmStartToggle == null || startBtn == null)
            throw new InvalidOperationException("ExperimentConfig UI references are incomplete.");

        if (runtimeConfig.scaleOptions == null || runtimeConfig.scaleOptions.Count == 0)
            throw new InvalidOperationException("Runtime Config has no scale options.");
        if (runtimeConfig.roundPresets == null || runtimeConfig.roundPresets.Count == 0)
            throw new InvalidOperationException("Runtime Config has no round presets.");
        if (scaleOptionButtons == null || scaleOptionButtons.Count != runtimeConfig.scaleOptions.Count)
            throw new InvalidOperationException(
                $"Scale button count ({scaleOptionButtons?.Count ?? 0}) must match scale option count ({runtimeConfig.scaleOptions.Count}).");
        if (roundPresetButtons == null || roundPresetButtons.Count != runtimeConfig.roundPresets.Count)
            throw new InvalidOperationException(
                $"Round preset button count ({roundPresetButtons?.Count ?? 0}) must match round preset count ({runtimeConfig.roundPresets.Count}).");
        if (scaleOptionButtons.Exists(b => b == null))
            throw new InvalidOperationException("Scale option buttons contain null entries.");
        if (roundPresetButtons.Exists(b => b == null))
            throw new InvalidOperationException("Round preset buttons contain null entries.");
        if (string.IsNullOrWhiteSpace(runtimeConfig.feedbackObjectiveKey))
            throw new InvalidOperationException("Runtime Config feedback objective key cannot be empty.");
        if (string.IsNullOrWhiteSpace(runtimeConfig.targetSliderName) && likertSlider == null)
            throw new InvalidOperationException(
                "Runtime Config target slider name is empty and no Slider reference is assigned.");
    }

    private void ClampSelectionIndices()
    {
        _selectedScaleIndex = Mathf.Clamp(
            _selectedScaleIndex < 0 ? runtimeConfig.defaultScaleIndex : _selectedScaleIndex,
            0,
            runtimeConfig.scaleOptions.Count - 1);
        _selectedRoundPresetIndex = Mathf.Clamp(
            _selectedRoundPresetIndex < 0 ? runtimeConfig.defaultRoundPresetIndex : _selectedRoundPresetIndex,
            0,
            runtimeConfig.roundPresets.Count - 1);
    }

    private void BindScaleButtons()
    {
        for (int i = 0; i < scaleOptionButtons.Count; i++)
        {
            int index = i;
            scaleOptionButtons[i].onClick.RemoveAllListeners();
            scaleOptionButtons[i].onClick.AddListener(() =>
            {
                _selectedScaleIndex = index;
                HighlightScale(index);
            });
        }
    }

    private void BindRoundButtons()
    {
        for (int i = 0; i < roundPresetButtons.Count; i++)
        {
            int index = i;
            roundPresetButtons[i].onClick.RemoveAllListeners();
            roundPresetButtons[i].onClick.AddListener(() =>
            {
                _selectedRoundPresetIndex = index;
                HighlightRoundPreset(index);
            });
        }
    }

    private void HighlightScale(int selectedIndex)
    {
        for (int i = 0; i < scaleOptionButtons.Count; i++)
            SetButtonColor(scaleOptionButtons[i], i == selectedIndex ? _selectedColor : _defaultColor);
    }

    private void HighlightRoundPreset(int selectedIndex)
    {
        for (int i = 0; i < roundPresetButtons.Count; i++)
            SetButtonColor(roundPresetButtons[i], i == selectedIndex ? _selectedColor : _defaultColor);
    }

    private static void SetButtonColor(Button btn, Color color)
    {
        var colors = btn.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        btn.colors = colors;
    }

    private void OnStartClicked()
    {
        _experimentStarted = true;
        ApplyConfig();

        if (boManager.pythonStarter != null)
            boManager.pythonStarter.enabled = true;

        configPanel.SetActive(false);
        boManager.welcomePanel.SetActive(true);
        if (boManager.initialized)
            boManager.nextButton.SetActive(true);
    }

    private void ApplyConfig()
    {
        ExperimentScaleOption selectedScale = runtimeConfig.scaleOptions[_selectedScaleIndex];
        ExperimentRoundPreset selectedRounds = runtimeConfig.roundPresets[_selectedRoundPresetIndex];
        string objectiveKey = runtimeConfig.feedbackObjectiveKey.Trim();
        string sliderName = string.IsNullOrWhiteSpace(runtimeConfig.targetSliderName)
            ? string.Empty
            : runtimeConfig.targetSliderName.Trim();

        Slider slider = ResolveTargetSlider(sliderName);
        if (slider == null)
            throw new InvalidOperationException(
                $"Target slider '{sliderName}' was not found. Assign likertSlider or fix Runtime Config targetSliderName.");

        ApplyScaleToSlider(slider, selectedScale.maxValue);
        ApplyScaleToObjective(objectiveKey, selectedScale.maxValue);

        boManager.warmStart = _warmStart;
        boManager.numSamplingIterations = Mathf.Max(0, selectedRounds.samplingRounds);
        boManager.numOptimizationIterations = Mathf.Max(0, selectedRounds.optimizationRounds);
        boManager.totalIterations = boManager.warmStart
            ? boManager.numOptimizationIterations
            : boManager.numSamplingIterations + boManager.numOptimizationIterations;
        boManager.SetExperimentSessionContext(selectedScale.maxValue, objectiveKey, sliderName);

        if (boManager.warmStart)
        {
            boManager.initialParametersDataPath = "warmstart_params.csv";
            boManager.initialObjectivesDataPath = "warmstart_objectives.csv";
        }
    }

    private Slider ResolveTargetSlider(string sliderName)
    {
        if (likertSlider != null)
            return likertSlider;

        foreach (var slider in Resources.FindObjectsOfTypeAll<Slider>())
        {
            if (string.Equals(slider.gameObject.name, sliderName, StringComparison.Ordinal))
                return slider;
        }

        return null;
    }

    private void ApplyScaleToSlider(Slider slider, int maxValue)
    {
        slider.minValue = 1;
        slider.maxValue = maxValue;
        slider.wholeNumbers = true;
        slider.value = (maxValue + 1) / 2f;
    }

    private void ApplyScaleToObjective(string objectiveKey, int maxValue)
    {
        bool found = false;
        foreach (var obj in boManager.objectives)
        {
            if (!string.Equals(obj.key, objectiveKey, StringComparison.Ordinal))
                continue;

            obj.value.lowerBound = 1;
            obj.value.upperBound = maxValue;
            found = true;
            break;
        }

        if (!found)
            throw new InvalidOperationException(
                $"Objective key '{objectiveKey}' was not found in BoForUnityManager.objectives.");
    }
}
