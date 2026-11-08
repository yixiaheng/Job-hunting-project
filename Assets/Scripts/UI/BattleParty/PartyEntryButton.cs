using Game.UI;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class PartyEntryButton : MonoBehaviour
{
    [SerializeField] private SceneTransitionMode transition = SceneTransitionMode.Fade;
    private UIManager manager;
    private UIPanel panel;
    private CanvasGroup group;
    private bool CanShow => manager != null && panel != null && panel.Config != null
        && panel.Config.id == UIId.ExplorationHUD && manager.IsUnobstructedHUD(panel.Handle);
    private void OnEnable()
    {
        manager = UIManager.Instance; panel = GetComponentInParent<UIPanel>(); group = GetComponent<CanvasGroup>();
        if (manager != null) manager.PresentationStateChanged += Refresh;
        Refresh();
    }
    private void OnDisable()
    { if (manager != null) manager.PresentationStateChanged -= Refresh; manager = null; }
    private void Refresh()
    { bool visible = CanShow; group.alpha = visible ? 1 : 0; group.interactable = group.blocksRaycasts = visible; }
    public void Open() { if (CanShow) PartySceneNavigator.TryEnter(transition); }
}
