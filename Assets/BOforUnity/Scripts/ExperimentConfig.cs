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
    private static bool _warmStart = false;
    private static bool _experimentStarted = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _likertMax = 5;
        _samplingRounds = 10;
        _warmStart = false;
        _experimentStarted = false;
    }

    void Awake()
    {
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
        startBtn.onClick.AddListener(OnStartClicked);

        // 读取 Toggle 初始状态
        _warmStart = warmStartToggle.isOn;

        HighlightScale(scale5Btn);
        HighlightRounds(rounds10Btn);
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
        UnityEngine.Debug.Log($"ApplyConfig: likertMax={_likertMax}, warmStart={_warmStart}, sampling={_samplingRounds}");

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

        boManager.numSamplingIterations = _samplingRounds;
        boManager.numOptimizationIterations = (_samplingRounds == 15) ? 0 : 5;
        boManager.warmStart = _warmStart;
        boManager.enableFinalDesignRound = true;

        boManager.totalIterations = _warmStart
            ? boManager.numOptimizationIterations
            : _samplingRounds + boManager.numOptimizationIterations;

        if (_warmStart)
        {
            boManager.initialParametersDataPath = "warmstart_params.csv";
            boManager.initialObjectivesDataPath = "warmstart_objectives.csv";
        }
    }
}