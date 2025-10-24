using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using Rocworks.Mqtt;

public class AdminScoreController : MonoBehaviour, IObserver
{
    [SerializeField] private Dropdown presetDropdown;
    [SerializeField] private Button addPresetButton;
    [SerializeField] private Slider scoreDurationweight;
    [SerializeField] private Slider scoreQualityweight;
    [SerializeField] private Slider scorePerformanceStreakweight;
    [SerializeField] private Slider scoreQualityStreakweight;
    [SerializeField] private Slider scoreDurationImprovementweight;
    [SerializeField] private Slider scoreWorkErrorweight;

    [SerializeField] private Text scoreDurationweightmin;
    [SerializeField] private Text scoreDurationweightmax;
    [SerializeField] private Text scoreQualityweightmin;
    [SerializeField] private Text scoreQualityweightmax;
    [SerializeField] private Text scorePerformanceStreakweightmin;
    [SerializeField] private Text scorePerformanceStreakweightmax;
    [SerializeField] private Text scoreQualityStreakweightmin;
    [SerializeField] private Text scoreQualityStreakweightmax;
    [SerializeField] private Text scoreDurationImprovementweightmin;
    [SerializeField] private Text scoreDurationImprovementweightmax;
    [SerializeField] private Text scoreWorkErrorweightmin;
    [SerializeField] private Text scoreWorkErrorweightmax;

    [SerializeField] private Text scoreDurationweightValue;
    [SerializeField] private Text scoreQualityweightValue;
    [SerializeField] private Text scorePerformanceStreakweightValue;
    [SerializeField] private Text scoreQualityStreakweightValue;
    [SerializeField] private Text scoreDurationImprovementweightValue;
    [SerializeField] private Text scoreWorkErrorweightValue;

    [SerializeField] private Button sendConfigButton;

    [SerializeField] private CanvasGroup adminPanel;



    private MsgHandler msgHandler;
    private List<Preset> presets = new List<Preset>(); // Single list to store both names and data
  // List to store preset data // List to store preset data

    // Start is called before the first frame update
    private void Start()
    {
        msgHandler = FindObjectOfType<MsgHandler>();

        if (msgHandler != null)
        {
            // Register this controller to listen for user data changes
            msgHandler.RegisterObserver("UserDataUpdate/ScorePresets", this);
            msgHandler.RegisterObserver("UserDataUpdate/Rights", this);
            addPresetButton.onClick.AddListener(AddNewPreset);
            sendConfigButton.onClick.AddListener(SendConfigToMQTT);  // Add listener for the send config button
        }

        // Add listener for dropdown selection changes
        presetDropdown.onValueChanged.AddListener(OnPresetSelected);

        // Initially hide the admin panel
        SetPanelVisibility(adminPanel, false);

         scoreDurationweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scoreDurationweight, scoreDurationweightValue, "score_duration_weight"); });
        scoreQualityweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scoreQualityweight, scoreQualityweightValue, "score_quality_weight"); });
        scorePerformanceStreakweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scorePerformanceStreakweight, scorePerformanceStreakweightValue, "score_performance_streak_bonus_weight"); });
        scoreQualityStreakweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scoreQualityStreakweight, scoreQualityStreakweightValue, "score_quality_streak_bonus_weight"); });
        scoreDurationImprovementweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scoreDurationImprovementweight, scoreDurationImprovementweightValue, "score_duration_improvement_weight"); });
        scoreWorkErrorweight.onValueChanged.AddListener(delegate { UpdateSliderTextAndSave(scoreWorkErrorweight, scoreWorkErrorweightValue, "score_workerror_weight"); });
    }



    private void OnDestroy()
    {
        if (msgHandler != null)
        {
            // Unsubscribe from all topics to avoid memory leaks
            msgHandler.RemoveObserver("UserDataUpdate/ScorePresets", this);
            msgHandler.RemoveObserver("UserDataUpdate/Rights", this);
        }
    }

public void OnNotify(string eventType, object data)
{
    Debug.Log("AdminPanelMessageReceived");

    if (eventType == "UserDataUpdate/Rights")
    {
        string userRights = data.ToString();
        Debug.Log($"Rights Updated: {userRights}");

        // Only show the admin panel if the user has Admin rights
        SetPanelVisibility(adminPanel, userRights == "Admin");
    }
    else if (eventType == "UserDataUpdate/ScorePresets")
    {
        JArray scorePresets = JArray.Parse(data.ToString());
        presets.Clear();  // Clear the old preset data
        int activePresetIndex = -1;  // Track the active preset index

        foreach (JObject preset in scorePresets)
        {
            string presetName = preset["preset_name"].ToString();
            presets.Add(new Preset(presetName, preset));

            // Set the active preset index
            if ((bool)preset["active_preset"])
            {
                SetSliderValues(preset);  // Set the sliders based on the active preset
                activePresetIndex = presets.Count - 1;  // Store the index of the active preset
            }
        }

        PopulatePresetDropdown(presets);  // Populate the dropdown

        if (activePresetIndex != -1)
        {
            presetDropdown.value = activePresetIndex;  // Set the active preset in the dropdown
        }

        Debug.Log("ScorePresets Updated");
    }
}

    // Function to toggle the visibility of the admin panel
    private void SetPanelVisibility(CanvasGroup panel, bool isVisible)
    {
        panel.alpha = isVisible ? 1 : 0;      // Set alpha to 1 (visible) or 0 (invisible)
        panel.interactable = isVisible;       // Enable/disable interactions
        panel.blocksRaycasts = isVisible;     // Block or allow clicks through the UI
    }

    // Handle setting slider values from preset data
   private void SetSliderValues(JObject presetData)
{
    // Set the sliders and display the corresponding text values
    scoreDurationweight.minValue = (float)presetData["min_duration_weight"];
    scoreDurationweight.maxValue = (float)presetData["max_duration_weight"];
    scoreDurationweight.value = (float)presetData["score_duration_weight"];
    scoreDurationweightValue.text = scoreDurationweight.value.ToString("0.00");

    scoreQualityweight.minValue = (float)presetData["min_quality_weight"];
    scoreQualityweight.maxValue = (float)presetData["max_quality_weight"];
    scoreQualityweight.value = (float)presetData["score_quality_weight"];
    scoreQualityweightValue.text = scoreQualityweight.value.ToString("0.00");

    scorePerformanceStreakweight.minValue = (float)presetData["min_performance_streak_weight"];
    scorePerformanceStreakweight.maxValue = (float)presetData["max_performance_streak_weight"];
    scorePerformanceStreakweight.value = (float)presetData["score_performance_streak_bonus_weight"];
    scorePerformanceStreakweightValue.text = scorePerformanceStreakweight.value.ToString("0.00");

    scoreQualityStreakweight.minValue = (float)presetData["min_quality_streak_weight"];
    scoreQualityStreakweight.maxValue = (float)presetData["max_quality_streak_weight"];
    scoreQualityStreakweight.value = (float)presetData["score_quality_streak_bonus_weight"];
    scoreQualityStreakweightValue.text = scoreQualityStreakweight.value.ToString("0.00");

    scoreDurationImprovementweight.minValue = (float)presetData["min_duration_improvement_weight"];
    scoreDurationImprovementweight.maxValue = (float)presetData["max_duration_improvement_weight"];
    scoreDurationImprovementweight.value = (float)presetData["score_duration_improvement_weight"];
    scoreDurationImprovementweightValue.text = scoreDurationImprovementweight.value.ToString("0.00");

    scoreWorkErrorweight.minValue = (float)presetData["min_workerror_weight"];
    scoreWorkErrorweight.maxValue = (float)presetData["max_workerror_weight"];
    scoreWorkErrorweight.value = (float)presetData["score_workerror_weight"];
    scoreWorkErrorweightValue.text = scoreWorkErrorweight.value.ToString("0.00");

    // Also update the min/max text values
    scoreDurationweightmin.text = presetData["min_duration_weight"].ToString();
    scoreDurationweightmax.text = presetData["max_duration_weight"].ToString();
    scoreQualityweightmin.text = presetData["min_quality_weight"].ToString();
    scoreQualityweightmax.text = presetData["max_quality_weight"].ToString();
    scorePerformanceStreakweightmin.text = presetData["min_performance_streak_weight"].ToString();
    scorePerformanceStreakweightmax.text = presetData["max_performance_streak_weight"].ToString();
    scoreQualityStreakweightmin.text = presetData["min_quality_streak_weight"].ToString();
    scoreQualityStreakweightmax.text = presetData["max_quality_streak_weight"].ToString();
    scoreDurationImprovementweightmin.text = presetData["min_duration_improvement_weight"].ToString();
    scoreDurationImprovementweightmax.text = presetData["max_duration_improvement_weight"].ToString();
    scoreWorkErrorweightmin.text = presetData["min_workerror_weight"].ToString();
    scoreWorkErrorweightmax.text = presetData["max_workerror_weight"].ToString();
}
   private void PopulatePresetDropdown(List<Preset> presets)
{
    presetDropdown.ClearOptions();
    List<string> options = new List<string>();

    foreach (Preset preset in presets)
    {
        options.Add(preset.Name);  // Use the Name property from the Preset object

        // Check if this preset is the active one and select it
        if ((bool)preset.Data["active_preset"])
        {
            presetDropdown.value = options.Count - 1; // Select active preset
        }
    }

    presetDropdown.AddOptions(options);
}

private void OnPresetSelected(int index)
{
    if (index < presets.Count)
    {
        SetSliderValues(presets[index].Data);  // Load the selected preset data from the Data property
    }
}



private void AddNewPreset()
{
 // Extract all existing preset numbers
    List<int> existingPresetNumbers = new List<int>();

    foreach (var preset in presets)
    {
        if (preset.Name.StartsWith("Preset:"))
        {
            if (int.TryParse(preset.Name.Substring(7), out int number) && number >= 1)
            {
                existingPresetNumbers.Add(number);
            }
        }
    }

    existingPresetNumbers.Sort();

    int newPresetNumber = 1;  // Start at 1
    foreach (int number in existingPresetNumbers)
    {
        if (number == newPresetNumber)
        {
            newPresetNumber++;  // If this number exists, move to the next
        }
        else
        {
            break;  // We've found a gap or reached the next available number
        }
    }


    // Create the new preset name
    string newPresetName = $"Preset:{newPresetNumber}";

    // Set the default values for the new preset
    float minValue = 0f;
    float maxValue = 5f;
    float defaultValue = 2.5f;

    // Create a new JObject to store the preset data
    JObject newPresetData = new JObject
    {
        { "preset_name", newPresetName },
        { "min_duration_weight", minValue },
        { "max_duration_weight", maxValue },
        { "score_duration_weight", defaultValue },
        { "min_quality_weight", minValue },
        { "max_quality_weight", maxValue },
        { "score_quality_weight", defaultValue },
        { "min_performance_streak_weight", minValue },
        { "max_performance_streak_weight", maxValue },
        { "score_performance_streak_bonus_weight", defaultValue },
        { "min_quality_streak_weight", minValue },
        { "max_quality_streak_weight", maxValue },
        { "score_quality_streak_bonus_weight", defaultValue },
        { "min_duration_improvement_weight", minValue },
        { "max_duration_improvement_weight", maxValue },
        { "score_duration_improvement_weight", defaultValue },
        { "min_workerror_weight", minValue },
        { "max_workerror_weight", maxValue },
        { "score_workerror_weight", defaultValue },
        { "active_preset", false }
    };

    // Add the new preset to the list
    presets.Add(new Preset(newPresetName, newPresetData));

    // Add the new preset to the dropdown
    presetDropdown.options.Add(new Dropdown.OptionData(newPresetName));
    presetDropdown.value = presetDropdown.options.Count - 1;  // Select the new preset in the dropdown

    // Notify the system or server of the new preset (this is a placeholder; you'll need to handle backend logic)
    Debug.Log($"Added new preset: {newPresetName}");
}

private void UpdateSliderTextAndSave(Slider slider, Text valueText, string key)
{
    valueText.text = slider.value.ToString("0.00");  // Update the text

    // Update the currently selected preset's data in the list of presets
    if (presetDropdown.value < presets.Count)
    {
        // Access the Data property of the current preset
        JObject currentPresetData = presets[presetDropdown.value].Data;
        currentPresetData[key] = slider.value;  // Update the value in the preset data
    }
}
private void SendConfigToMQTT()
{// Get the index of the currently selected preset in the dropdown
    int selectedIndex = presetDropdown.value;

    // Loop through all presets and update the active_preset flag
    for (int i = 0; i < presets.Count; i++)
    {
        if (i == selectedIndex)
        {
            // Set the active_preset for the currently selected preset
            presets[i].Data["active_preset"] = true;
        }
        else
        {
            // Set active_preset to false for all other presets
            presets[i].Data["active_preset"] = false;
        }
    }
    // Create the message to send (in your desired format)
    JArray presetsArray = new JArray();

    foreach (Preset preset in presets)
    {
        // Add each preset's data to the array
        presetsArray.Add(preset.Data);
    }

    JObject message = new JObject
        {
            {"presets", presetsArray},
            {"mqtt", true}
        };
    // Send the message via MQTT
    if (msgHandler != null)
    {
        msgHandler.NotifyObservers("MVG_ESCH/PRODUCTION/MW/KST/0001/GAMING/SCOREPRESETS/UPDATE", message);
        Debug.Log("Presets configuration sent via MQTT.");
    }
}
}
public class Preset
{
    public string Name { get; set; }
    public JObject Data { get; set; }

    public Preset(string name, JObject data)
    {
        Name = name;
        Data = data;
    }
}
