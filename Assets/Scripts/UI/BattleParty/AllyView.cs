using Battle.Events;
using Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllyView : MonoBehaviour
{
    [SerializeField] private GameObject selectedSign;
    [SerializeField] private GameObject sortSign;
    [SerializeField] private Image allyImage;
    [SerializeField] private Button chooseButton;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Color selectedColor = new(1f, 0.96f, 0.72f, 1f);

    private UnitConfig unit;
    private Color normalImageColor;
    private bool hasNormalImageColor;

    private void Awake()
    {
        if (chooseButton != null)
            chooseButton.onClick.AddListener(Selected);
    }

    public void Bind(UnitConfig config)
    {
        unit = config;

        if (nameText != null)
            nameText.text = unit != null ? unit.displayName : string.Empty;

        if (chooseButton != null)
            chooseButton.interactable = unit != null;

        if (allyImage != null && !hasNormalImageColor)
        {
            normalImageColor = allyImage.color;
            hasNormalImageColor = true;
        }

        Refresh(false);
    }

    public void Refresh(
        bool isInFormation,
        bool isSelected = false,
        int sortPosition = 0,
        bool showFormationSign = true)
    {
        if (selectedSign != null)
            selectedSign.SetActive(showFormationSign && isInFormation);

        if (sortSign != null)
            sortSign.SetActive(sortPosition > 0);

        if (sortSign != null)
        {
            TextMeshProUGUI positionText = sortSign.GetComponentInChildren<TextMeshProUGUI>(true);
            if (positionText != null)
                positionText.text = sortPosition > 0 ? sortPosition.ToString() : string.Empty;
        }

        if (allyImage != null && hasNormalImageColor)
        {
            Color tint = selectedColor;
            tint.a = normalImageColor.a;
            allyImage.color = isSelected ? tint : normalImageColor;
        }
    }

    public void Selected()
    {
        if (unit != null)
            GameEvents.Battle.Publish(new AllySlectionEvent(unit, this));
    }

    private void OnDestroy()
    {
        if (chooseButton != null)
            chooseButton.onClick.RemoveListener(Selected);
    }
}
