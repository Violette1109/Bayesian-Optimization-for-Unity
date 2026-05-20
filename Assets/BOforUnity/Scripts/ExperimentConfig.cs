using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BOforUnity;

public class ExperimentConfig : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 🆕  User ID Panel (shown FIRST, before config)
    // ─────────────────────────────────────────────
    [Header("User ID Panel UI")]
    [Tooltip("The new panel that asks for a User ID before the config screen.")]
    public GameObject userIdPanel;

    [Tooltip("InputField where the experimenter / participant types their ID.")]
    public TMP_InputField userIdInputField;   // Use InputField (Legacy) if you are not using TextMeshPro

    [Tooltip("'Continue' button on the User ID panel.")]
    public Button userIdContinueBtn;

    // ─────────────────────────────────────────────
    // Config Panel UI  (unchanged)
    // ─────────────────────────────────────────────
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

    [Header("Dynamic References (Data-Driven)")]
    [Tooltip("直接拖入受試者評分用的 Slider，不再用名字字串去撈")]
    public Slider evaluationSlider;

    [Tooltip("請輸入妳在 BoManager 裡設定的問卷 Objective Key (例: mental_demand)")]
    public string targetObjectiveKey = "mental_demand";

    [Tooltip("當選擇 10 輪 Sampling 時，對應的 Optimization 輪數")]
    public int optimizationRoundsFor10 = 5;

    [Tooltip("當選擇 15 輪 Sampling 時，對應的 Optimization 輪數")]
    public int optimizationRoundsFor15 = 0;

    [Header("Manager References")]
    public BoForUnityManager boManager;

    // ─────────────────────────────────────────────
    // Private state
    // ─────────────────────────────────────────────
    private readonly Color _selectedColor = new Color(0.498f, 0.467f, 0.867f);
    private readonly Color _defaultColor  = new Color(0.9f, 0.9f, 0.9f);

    private static int    _likertMax          = 5;
    private static int    _samplingRounds     = 10;
    private static bool   _warmStart          = false;
    private static bool   _randomAllocation   = false;
    private static bool   _experimentStarted  = false;

    // 🆕  Persisted across scene reloads (domain-reload safe)
    private static string _userId = "";

    // ─────────────────────────────────────────────
    // Reset statics on domain reload (Editor play-mode safety)
    // ─────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _likertMax         = 5;
        _samplingRounds    = 10;
        _warmStart         = false;
        _randomAllocation  = false;
        _experimentStarted = false;
        _userId            = "";
    }

    // ─────────────────────────────────────────────
    // Awake – control initial panel visibility
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (!_experimentStarted)
        {
            // ── First-time launch: show User ID panel first ──
            userIdPanel.SetActive(true);
            configPanel.SetActive(false);
            boManager.welcomePanel.SetActive(false);
            boManager.nextButton.SetActive(false);

            if (boManager.pythonStarter != null)
                boManager.pythonStarter.enabled = false;
        }
        else
        {
            // ── Scene has reloaded mid-experiment: skip both pre-panels ──
            userIdPanel.SetActive(false);
            configPanel.SetActive(false);
            ApplyConfig();
        }
    }

    // ─────────────────────────────────────────────
    // Start – wire up all button / toggle listeners
    // ─────────────────────────────────────────────
    void Start()
    {
        if (_experimentStarted) return;

        // ── User ID panel ──
        userIdContinueBtn.onClick.AddListener(OnUserIdContinueClicked);

        // Pre-populate the field if a static value was already set
        // (useful if the scene reloads before _experimentStarted flips)
        if (!string.IsNullOrEmpty(_userId) && userIdInputField != null)
            userIdInputField.text = _userId;

        // ── Scale buttons ──
        scale5Btn  .onClick.AddListener(() => { SetScale(5);   HighlightScale(scale5Btn);   });
        scale20Btn .onClick.AddListener(() => { SetScale(20);  HighlightScale(scale20Btn);  });
        scale100Btn.onClick.AddListener(() => { SetScale(100); HighlightScale(scale100Btn); });

        // ── Round buttons ──
        rounds10Btn.onClick.AddListener(() =>
        {
            if (_randomAllocation) return;
            SetRounds(10);
            HighlightRounds(rounds10Btn);
        });

        rounds15Btn.onClick.AddListener(() =>
        {
            if (_randomAllocation) return;
            SetRounds(15);
            HighlightRounds(rounds15Btn);
        });

        // ── Toggles ──
        warmStartToggle       .onValueChanged.AddListener(val => _warmStart = val);
        randomAllocationToggle.onValueChanged.AddListener(OnRandomAllocationChanged);

        // ── Start button ──
        startBtn.onClick.AddListener(OnStartClicked);

        // Sync toggle state → statics
        _warmStart        = warmStartToggle.isOn;
        _randomAllocation = randomAllocationToggle.isOn;

        // Default highlights
        HighlightScale (scale5Btn);
        HighlightRounds(rounds10Btn);
    }

    // ─────────────────────────────────────────────
    // 🆕  User ID continue handler
    // ─────────────────────────────────────────────
    void OnUserIdContinueClicked()
    {
        // ── Validate ──
        string trimmed = userIdInputField != null
            ? userIdInputField.text.Trim()
            : "";

        if (string.IsNullOrEmpty(trimmed))
        {
            Debug.LogWarning("[ExperimentConfig] User ID is empty – please enter an ID before continuing.");
            // Optionally: flash the field red, show an error label, etc.
            return;
        }

        // ── Persist & apply ──
        _userId = trimmed;
        boManager.userId = _userId;

        Debug.Log($"[ExperimentConfig] User ID set to: '{_userId}'");

        // ── Advance to config panel ──
        userIdPanel .SetActive(false);
        configPanel .SetActive(true);
    }

    // ─────────────────────────────────────────────
    // Scale / round helpers  (unchanged)
    // ─────────────────────────────────────────────
    void SetScale(int val)               => _likertMax      = val;
    void SetRounds(int samplingVal)      => _samplingRounds = samplingVal;

    void OnRandomAllocationChanged(bool isOn)
    {
        _randomAllocation = isOn;

        if (isOn)
        {
            _samplingRounds = 15;
            HighlightRounds(rounds15Btn);
            rounds10Btn.interactable = false;
            rounds15Btn.interactable = false;
        }
        else
        {
            rounds10Btn.interactable = true;
            rounds15Btn.interactable = true;
            _samplingRounds = 10;
            HighlightRounds(rounds10Btn);
        }
    }

    // ─────────────────────────────────────────────
    // Highlight helpers  (unchanged)
    // ─────────────────────────────────────────────
    void HighlightScale(Button selected)
    {
        SetButtonColor(scale5Btn,   _defaultColor);
        SetButtonColor(scale20Btn,  _defaultColor);
        SetButtonColor(scale100Btn, _defaultColor);
        SetButtonColor(selected,    _selectedColor);
    }

    void HighlightRounds(Button selected)
    {
        SetButtonColor(rounds10Btn, _defaultColor);
        SetButtonColor(rounds15Btn, _defaultColor);
        SetButtonColor(selected,    _selectedColor);
    }

    void SetButtonColor(Button btn, Color color)
    {
        var colors = btn.colors;
        colors.normalColor  = color;
        colors.selectedColor = color;
        btn.colors = colors;
    }

    // ─────────────────────────────────────────────
    // Start button handler  (unchanged logic)
    // ─────────────────────────────────────────────
    void OnStartClicked()
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

    // ─────────────────────────────────────────────
    // ApplyConfig  (unchanged, with userId re-applied for safety)
    // ─────────────────────────────────────────────
    void ApplyConfig()
    {
        // Re-apply userId in case ApplyConfig is called on scene reload
        if (!string.IsNullOrEmpty(_userId))
            boManager.userId = _userId;

        Debug.Log(
            $"[Data-Driven Config] userId={_userId}, likertMax={_likertMax}, " +
            $"warmStart={_warmStart}, sampling={_samplingRounds}, " +
            $"randomAllocation={_randomAllocation}"
        );

        // ── Slider ──
        if (evaluationSlider != null)
        {
            evaluationSlider.minValue    = 1;
            evaluationSlider.maxValue    = _likertMax;
            evaluationSlider.wholeNumbers = true;
            evaluationSlider.value       = (_likertMax + 1) / 2;
        }
        else
        {
            Debug.LogWarning("ExperimentConfig: evaluationSlider 未在 Inspector 中指派！");
        }

        // ── Objective bounds ──
        bool foundObjective = false;
        foreach (var obj in boManager.objectives)
        {
            if (obj.key == targetObjectiveKey)
            {
                obj.value.lowerBound = 1;
                obj.value.upperBound = _likertMax;
                foundObjective = true;
                break;
            }
        }

        if (!foundObjective)
            Debug.LogWarning($"ExperimentConfig: 在 BoManager 中找不到對應的 Objective Key: '{targetObjectiveKey}'");

        // ── Condition / Group IDs ──
        if      (_likertMax == 5)   boManager.conditionId = "1";
        else if (_likertMax == 20)  boManager.conditionId = "2";
        else if (_likertMax == 100) boManager.conditionId = "3";

        if      (_samplingRounds == 10) boManager.groupId = "1";
        else if (_samplingRounds == 15) boManager.groupId = "2";

        // ── Iteration counts ──
        if (_randomAllocation)
        {
            boManager.numSamplingIterations     = 15;
            boManager.numOptimizationIterations = 0;
            boManager.enableFinalDesignRound    = false;
        }
        else
        {
            boManager.numSamplingIterations = _samplingRounds;
            boManager.numOptimizationIterations =
                (_samplingRounds == 15) ? optimizationRoundsFor15 : optimizationRoundsFor10;
            boManager.enableFinalDesignRound = true;
        }

        boManager.warmStart = _warmStart;

        boManager.totalIterations = _warmStart
            ? boManager.numOptimizationIterations
            : boManager.numSamplingIterations + boManager.numOptimizationIterations;

        if (_warmStart)
        {
            boManager.initialParametersDataPath = "warmstart_params.csv";
            boManager.initialObjectivesDataPath  = "warmstart_objectives.csv";
        }
    }
}