using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro; // Required for Reflection

[RequireComponent(typeof(Slider))]
public class SettingSlider : MonoBehaviour
{
    [Header("Configuration")]
    public string fieldName;

    public TextMeshProUGUI textValue;
    
    private BoidSettingsBridge _bridge; 
    private Slider _slider; 
    private bool _isUserInteracting;
    private FieldInfo _targetField;

    void Start()
    {
        // We want the sliders to be able to update the values in our BoidSettingsBridge
        // but we also want to be able to update BoidSettingsBridge from elsewhere (e.g. on reset, reading from config file, etc)
        // and the sliders must updat to refelct that. This is a hacky way around that, we get a reference to the boidsettings and check for updates
        // every frame. If the values have change we update the slider. If we are interacting with the slider the slider gets to override the value
        
        _slider = GetComponent<Slider>();
        if (_bridge == null) _bridge = FindFirstObjectByType<BoidSettingsBridge>();
        
        _targetField = typeof(BoidSettingsBridge).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);

        if (_targetField == null)
        {
            Debug.LogError($"Could not find field '{fieldName}' on BoidSettingsBridge");
            enabled = false;
            return;
        }
        _slider.onValueChanged.AddListener(OnSliderValueChanged);
        SyncFromDataToUI();
    }

    void Update()
    {
        if (!_isUserInteracting) SyncFromDataToUI();
    }

    private void OnSliderValueChanged(float value)
    {
        _isUserInteracting = true;
        if (_bridge != null && _targetField != null)
        {
            // Set the value dynamically
            _targetField.SetValue(_bridge, value);
            textValue.SetText(value.ToString("F2"));
        }
        _isUserInteracting = false;
    }

    private void SyncFromDataToUI()
    {
        // Get the value dynamically
        float currentValue = (float)_targetField.GetValue(_bridge);

        if (Mathf.Abs(_slider.value - currentValue) > 0.001f)
        {
            _slider.SetValueWithoutNotify(currentValue);
            textValue.SetText(currentValue.ToString("F2"));
        }
    }
}