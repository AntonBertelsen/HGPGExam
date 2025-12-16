using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

[RequireComponent(typeof(Toggle))]
public class SettingToggle : MonoBehaviour
{
    [Header("Configuration")]
    public string fieldName;

    private BoidSettingsBridge _bridge;
    private Toggle _toggle;
    private FieldInfo _targetField;

    void Start()
    {
        _toggle = GetComponent<Toggle>();
        
        if (_bridge == null) _bridge = FindFirstObjectByType<BoidSettingsBridge>();

        _targetField = typeof(BoidSettingsBridge).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        
        _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        
        SyncFromDataToUI();
    }

    void Update()
    {
        SyncFromDataToUI();
    }

    private void OnToggleValueChanged(bool value)
    {
        if (_bridge != null && _targetField != null)
        {
            _targetField.SetValue(_bridge, value);
        }
    }

    private void SyncFromDataToUI()
    {
        if (_targetField == null || _bridge == null) return;
        
        bool currentValue = (bool)_targetField.GetValue(_bridge);

        if (_toggle.isOn != currentValue)
        {
            _toggle.SetIsOnWithoutNotify(currentValue);
        }
    }
}