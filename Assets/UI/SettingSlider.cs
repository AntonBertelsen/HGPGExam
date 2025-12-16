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
    private bool _isInteger;

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
        if (_targetField.FieldType == typeof(int))
        {
            _isInteger = true;
            _slider.wholeNumbers = true; // Force slider to snap to integers
        }
        else if (_targetField.FieldType == typeof(float))
        {
            _isInteger = false;
            _slider.wholeNumbers = false;
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
            if (_isInteger)
            {
                int intVal = Mathf.RoundToInt(value);
                _targetField.SetValue(_bridge, intVal);
                if(textValue != null) textValue.SetText(intVal.ToString());
            }
            else
            {
                _targetField.SetValue(_bridge, value);
                if(textValue != null) textValue.SetText(value.ToString("F2"));
            }
        }
        _isUserInteracting = false;
    }

    private void SyncFromDataToUI()
    {
        // Get the value dynamically
        float currentValue;
        
        if (_isInteger)
        {
            int val = (int)_targetField.GetValue(_bridge);
            currentValue = (float)val;
        }
        else
        {
            currentValue = (float)_targetField.GetValue(_bridge);
        }

        if (Mathf.Abs(_slider.value - currentValue) > 0.001f)
        {
            if (_isInteger)
            {
                int val = (int)_targetField.GetValue(_bridge);
                _slider.SetValueWithoutNotify(val);
                if(textValue != null) textValue.SetText(val.ToString());
            }
            else
            {
                _slider.SetValueWithoutNotify(currentValue);
                textValue.SetText(currentValue.ToString("F2"));                
            }
        }
    }
}