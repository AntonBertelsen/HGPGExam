using UnityEngine;

public class CollapseSettingsPanel : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string _boolParamName = "IsExpanded";

    public void SetMenuState(bool isExpanded)
    {
        if (_animator != null)
        {
            _animator.SetBool(_boolParamName, isExpanded);
        }
    }
}
