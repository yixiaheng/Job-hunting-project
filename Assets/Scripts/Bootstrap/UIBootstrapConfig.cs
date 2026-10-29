using Game.UI;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Bootstrap Config", fileName = "UIBootstrapConfig")]
public sealed class UIBootstrapConfig : ScriptableObject
{
    [SerializeField] private UIManager runtimePrefab;

    public UIManager RuntimePrefab => runtimePrefab;
}
