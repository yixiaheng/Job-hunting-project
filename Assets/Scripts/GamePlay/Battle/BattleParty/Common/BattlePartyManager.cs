using System.Collections.Generic;
using Battle.SaveData;
using UnityEngine;

public class BattlePartyManager : MonoBehaviour
{
    public static BattlePartyManager Instance{ get; private set;}

    [SerializeField] private UnitConfig[] availableAllies;
    private BattleFormation formation;

    public IReadOnlyList<UnitConfig> AvailableAllies => availableAllies;
    public BattleFormation Formation => formation;


    

    void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(gameObject);


        formation = new BattleFormation();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
