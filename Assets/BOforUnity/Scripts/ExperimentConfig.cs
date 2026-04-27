using UnityEngine;
using UnityEngine.UI;
using BOforUnity;

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
    public Button startBtn;

    [Header("References")]
    public Slider likertSlider;
    public BoForUnityManager boManager;

    private readonly Color _selectedColor = new Color(0.498f, 0.467f, 0.867f);
    private readonly Color _defaultColor = new Color(0.9f, 0.9f, 0.9f);

    private static int _likertMax = 5;
    private static int _samplingRounds = 10;
    private static int _optimizationRounds = 5;
    private static bool _warmStart = true;
    private static bool _experimentStarted = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _likertMax = 5;
        _samplingRounds = 10;
        _optimizationRounds = 5;
        _warmStart = true;
        _experimentStarted = false;
    }

    void Awake()
    {
        if (!_experimentStarted)
        {
            configPanel.SetActive(true);
            boManager.welcomePanel.SetActive(false);
            boManager.nextButton.SetActive(false);
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
        rounds10Btn.onClick.AddListener(() => { SetRounds(10, 5); HighlightRounds(rounds10Btn); });
        rounds15Btn.onClick.AddListener(() => { SetRounds(15, 0); HighlightRounds(rounds15Btn); });
        warmStartToggle.onValueChanged.AddListener(val => _warmStart = val);
        startBtn.onClick.AddListener(OnStartClicked);

        HighlightScale(scale5Btn);
        HighlightRounds(rounds10Btn);
    }

    void SetScale(int val) { _likertMax = val; }

    void SetRounds(int samplingVal, int optimizationVal)
    {
        _samplingRounds = samplingVal;
        _optimizationRounds = optimizationVal;
    }

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
        configPanel.SetActive(false);
        boManager.welcomePanel.SetActive(true);
        if (boManager.initialized)
            boManager.nextButton.SetActive(true);
    }

    void ApplyConfig()
    {
    // 重新找 slider
    Slider s = null;
    foreach (var slider in Resources.FindObjectsOfTypeAll<Slider>())
    {
        if (slider.gameObject.name == "slider 1-100")
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

    if (boManager.objectives.Count > 0)
    {
        boManager.objectives[0].value.lowerBound = 1;
        boManager.objectives[0].value.upperBound = _likertMax;
    }

    boManager.numOptimizationIterations = (_samplingRounds == 15) ? 0 : 5;
    boManager.warmStart = _warmStart;
    boManager.enableFinalDesignRound = true;

    if (!_warmStart)
    {
        boManager.numSamplingIterations = _samplingRounds;
    }

    // Warm Start CSV 路径
    if (_warmStart)
    {
        boManager.initialParametersDataPath = "warmstart_params.csv";
        boManager.initialObjectivesDataPath = "warmstart_objectives.csv";
    }
    }
}