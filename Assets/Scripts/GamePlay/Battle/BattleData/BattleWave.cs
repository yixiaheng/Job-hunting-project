using UnityEngine;
using System;

//战斗波次，每个波次有一定的怪物
//Todo:怪物不一定是一波波的，有可能会补齐怪物，怪物可能会随机出现
[System.Serializable]
public class BattleWave
{
    public BattleUnit[] enemies;
}