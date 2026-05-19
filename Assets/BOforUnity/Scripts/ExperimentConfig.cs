using UnityEngine;
using UnityEngine.UI;
using BOforUnity;
using TMPro;

public class ExperimentConfig : MonoBehaviour
{
    [Header("Config Panel UI")]
    public GameObject configPanel;
    public Button scale5Btn;
    public Button scale20Btn;
    public Button scale100Btn;
    public Button rounds10Btn;
    public Button rounds15Btn;
    public Toggle warmStartToggle;
    public Toggle randomAllocationToggle;
    public Button startBtn;

    [Header("References")]
    public Slider likertSlider;
    public BoForUnityManager boManager;

    private readonly Color _selectedColor = new Color(0.498f, 0.467f, 0.867f);
    private readonly Color _defaultColor = new Color(0.9f, 0.9f, 0.9f);

    private static int _likertMax = 5;
    private static int _samplingRounds = 10;
    private static bool _warmStart = false;
    private static bool _randomAllocation = false;
    private static bool _experimentStarted = false;
    private const int RandomAllocationSamplingRounds = 15;
    private const int GuidedOptimizationRounds = 5;
    private const float GeneratedToggleYOffset = -80f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _likertMax = 5;
        _samplingRounds = 10;
        _warmStart = false;
        _randomAllocation = false;
        _experimentStarted = false;
    }

    void Awake()
    {
        EnsureRandomAllocationToggle();

        if (!_experimentStarted)
        {
            configPanel.SetActive(true);
            boManager.welcomePanel.SetActive(false);
            boManager.nextButton.SetActive(false);

            // 暂停 Python，等用户选完参数再启动
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
        if (_experimentStarted) return;

        scale5Btn.onClick.AddListener(() => { SetScale(5); HighlightScale(scale5Btn); });
        scale20Btn.onClick.AddListener(() => { SetScale(20); HighlightScale(scale20Btn); });
        scale100Btn.onClick.AddListener(() => { SetScale(100); HighlightScale(scale100Btn); });
        rounds10Btn.onClick.AddListener(() => { SetRounds(10); HighlightRounds(rounds10Btn); });
        rounds15Btn.onClick.AddListener(() => { SetRounds(15); HighlightRounds(rounds15Btn); });
        warmStartToggle.onValueChanged.AddListener(val => _warmStart = val);
        if (randomAllocationToggle != null)
            randomAllocationToggle.onValueChanged.AddListener(OnRandomAllocationChanged);
        startBtn.onClick.AddListener(OnStartClicked);

        // 读取 Toggle 初始状态
        _warmStart = warmStartToggle.isOn;
        if (randomAllocationToggle != null)
        {
            randomAllocationToggle.isOn = _randomAllocation;
            OnRandomAllocationChanged(_randomAllocation);
        }

        HighlightScale(scale5Btn);
        HighlightRounds(_samplingRounds == RandomAllocationSamplingRounds ? rounds15Btn : rounds10Btn);
        UpdateRandomAllocationUiState();
    }

    void SetScale(int val) { _likertMax = val; }
    void SetRounds(int samplingVal) { _samplingRounds = samplingVal; }

    void HighlightScale(Button selected)
    {
        SetButtonColor(scale5Btn, _defaultColor);
        SetButtonColor(scale20Btn, _defaultColor);
        SetButtonColor(scale100Btn, _defaultColor);
        SetButtonColor(selected, _selectedColor);
    }

    void HighlightRounds(Button selected)
    {
        SetButtonColor(rounds10Btn, _defaultColor);
        SetButtonColor(rounds15Btn, _defaultColor);
        SetButtonColor(selected, _selectedColor);
    }

    void SetButtonColor(Button btn, Color color)
    {
        var colors = btn.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        btn.colors = colors;
    }

    void OnStartClicked()
    {
        _experimentStarted = true;
        ApplyConfig();

        // 设好参数后才启动 Python
        if (boManager.pythonStarter != null)
            boManager.pythonStarter.enabled = true;

        configPanel.SetActive(false);
        boManager.welcomePanel.SetActive(true);
        if (boManager.initialized)
            boManager.nextButton.SetActive(true);
    }

    void ApplyConfig()
    {
        int effectiveSamplingRounds = _randomAllocation ? RandomAllocationSamplingRounds : _samplingRounds;
        int effectiveOptimizationRounds = GetEffectiveOptimizationRounds(effectiveSamplingRounds);
        bool effectiveWarmStart = _randomAllocation ? false : _warmStart;
        bool enableFinalDesignRound = !_randomAllocation;

        UnityEngine.Debug.Log(
            $"ApplyConfig: likertMax={_likertMax}, warmStart={effectiveWarmStart}, " +
            $"sampling={effectiveSamplingRounds}, randomAllocation={_randomAllocation}");

        // 重新找 slider
        Slider s = null;
        foreach (var slider in Resources.FindObjectsOfTypeAll<Slider>())
        {
            if (slider.gameObject.name == "SliderBar")
            {
                s = slider;
                break;
            }
        }

        if (s != null)
        {
            s.minValue = 1;
            s.maxValue = _likertMax;
            s.wholeNumbers = true;
            s.value = (_likertMax + 1) / 2;
        }

        // 用名字找 mental_demand objective
        foreach (var obj in boManager.objectives)
        {
            if (obj.key == "mental_demand")
            {
                obj.value.lowerBound = 1;
                obj.value.upperBound = _likertMax;
                break;
            }
        }

        boManager.numSamplingIterations = effectiveSamplingRounds;
        boManager.numOptimizationIterations = effectiveOptimizationRounds;
        boManager.warmStart = effectiveWarmStart;
        boManager.enableFinalDesignRound = enableFinalDesignRound;

        boManager.totalIterations = effectiveWarmStart
            ? boManager.numOptimizationIterations
            : effectiveSamplingRounds + boManager.numOptimizationIterations;

        if (effectiveWarmStart)
        {
            boManager.initialParametersDataPath = "warmstart_params.csv";
            boManager.initialObjectivesDataPath = "warmstart_objectives.csv";
        }
    }

    void OnRandomAllocationChanged(bool isOn)
    {
        _randomAllocation = isOn;
        if (_randomAllocation)
        {
            _samplingRounds = RandomAllocationSamplingRounds;
            HighlightRounds(rounds15Btn);
        }

        UpdateRandomAllocationUiState();
    }

    void UpdateRandomAllocationUiState()
    {
        bool enableManualRoundSelection = !_randomAllocation;
        if (rounds10Btn != null)
            rounds10Btn.interactable = enableManualRoundSelection;
        if (rounds15Btn != null)
            rounds15Btn.interactable = enableManualRoundSelection;

        if (warmStartToggle != null)
        {
            warmStartToggle.interactable = !_randomAllocation;
            if (_randomAllocation && warmStartToggle.isOn)
                warmStartToggle.isOn = false;
        }
    }

    int GetEffectiveOptimizationRounds(int effectiveSamplingRounds)
    {
        if (_randomAllocation || effectiveSamplingRounds == RandomAllocationSamplingRounds)
            return 0;

        return GuidedOptimizationRounds;
    }

    void EnsureRandomAllocationToggle()
    {
        if (randomAllocationToggle != null || warmStartToggle == null)
            return;

        TextMeshProUGUI warmStartLabel = FindWarmStartLabel();
        if (warmStartLabel == null)
            return;

        randomAllocationToggle = Instantiate(warmStartToggle, warmStartToggle.transform.parent);
        randomAllocationToggle.gameObject.name = "Random Allocation Toggle";
        randomAllocationToggle.isOn = _randomAllocation;

        RectTransform warmToggleRect = warmStartToggle.GetComponent<RectTransform>();
        RectTransform randomToggleRect = randomAllocationToggle.GetComponent<RectTransform>();
        randomToggleRect.anchoredPosition = warmToggleRect.anchoredPosition + new Vector2(0f, GeneratedToggleYOffset);

        TextMeshProUGUI randomLabel = Instantiate(warmStartLabel, warmStartLabel.transform.parent);
        randomLabel.gameObject.name = "Random Allocation Label";
        randomLabel.text = "Random Allocation";
        randomLabel.rectTransform.anchoredPosition =
            warmStartLabel.rectTransform.anchoredPosition + new Vector2(0f, GeneratedToggleYOffset);
    }

    TextMeshProUGUI FindWarmStartLabel()
    {
        if (warmStartToggle == null || warmStartToggle.transform.parent == null)
            return null;

        RectTransform warmToggleRect = warmStartToggle.GetComponent<RectTransform>();
        TextMeshProUGUI bestMatch = null;
        float bestScore = float.MaxValue;

        foreach (TextMeshProUGUI text in warmStartToggle.transform.parent.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null || text.transform.parent != warmStartToggle.transform.parent)
                continue;

            Vector2 delta = text.rectTransform.anchoredPosition - warmToggleRect.anchoredPosition;
            if (delta.x >= 0f)
                continue;

            float score = Mathf.Abs(delta.y) * 1000f + Mathf.Abs(delta.x);
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestMatch = text;
        }

        return bestMatch;
    }
}
