using UnityEngine.UI;
using UnityEngine;
using Game.Events;
using Game;
using Battle.Events;

public class AllyView : MonoBehaviour
{
    [SerializeField] private GameObject Selected_Sign;
    [SerializeField] private GameObject Sort_Sign;
    [SerializeField] private Image AllyImage;
    [SerializeField] private Button ChooseButton;
    
    private UnitConfig unit;

    public void Bind(UnitConfig unitConfig)
    {
        if(unitConfig != null)
            unit = unitConfig;
        else
            Debug.LogError($"{gameObject} Bind Error");
    }

    public void Refresh()
    {
        
    }

    public void Selected()
    {
        //被选择的时候进行广播，其他人刷新选项，然后Manager知道选中的具体unit
        GameEvents.Battle?.Publish<AllySlectionEvent>(new AllySlectionEvent(unit));
    }
}
