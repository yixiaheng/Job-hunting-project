using Game.UI;
using UnityEngine;

[RequireComponent(typeof(UIHUDEntryVisibility))]
public sealed class PartyEntryButton : MonoBehaviour
{
    [SerializeField] private SceneTransitionMode transition = SceneTransitionMode.Fade;

    public void Open()
    {
        if (GetComponent<UIHUDEntryVisibility>().CanActivate)
            PartySceneNavigator.TryEnter(transition);
    }
}
