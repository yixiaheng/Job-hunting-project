using System.Collections.Generic;

namespace Battle.Logic
{
    //效果的类型
    public enum BattleEffectRecordType
    {
        Damage,
        Heal,
        Shield,
        Energy,
        SkillPoint,
        BuffApplied,
        BuffRemoved,
        ActionAdvanced,
        FollowUp
    }

    //效果记录，记录效果来源与所施加对象
    //以及
    public readonly struct BattleEffectRecord
    {
        public BattleEffectRecordType Type { get; }
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public int Amount { get; }  //数值
        public int Before { get; }  //数值变化前
        public int After { get; }   //变化后
        public string Key { get; }  //唯一标识
        public int EffectGroup { get; } //效果分组
        public int ShieldBefore { get; }//护盾之前
        public int ShieldAfter { get; } //护盾之后

        public BattleEffectRecord(BattleEffectRecordType type, BattleUnit source,
            BattleUnit target, int amount = 0, int before = 0, int after = 0, string key = null,
            int effectGroup = -1, int shieldBefore = -1, int shieldAfter = -1)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Before = before;
            After = after;
            Key = key;
            EffectGroup = effectGroup; ShieldBefore = shieldBefore; ShieldAfter = shieldAfter;
        }
        internal BattleEffectRecord InGroup(int group) => new(Type, Source, Target, Amount, Before, After, Key, group, ShieldBefore, ShieldAfter);
    }

    //战斗效果解析后的目标集合
    public sealed class BattleResolvedEffectTargets
    {
        public int Group { get; }   //分的组
        public IReadOnlyList<BattleUnit> Targets { get; }   //本次效果的目标集合
        public BattleResolvedEffectTargets(int group, IEnumerable<BattleUnit> targets)
        { 
            Group = group; 
            Targets = new List<BattleUnit>(targets).AsReadOnly(); 
        }
    }

    //战斗Action记录
    public sealed class BattleActionRecord
    {
        public long RequestId { get; internal set; }            //行动请求编号
        public BattleActionOrigin Origin { get; internal set; } 
        public int ActionSequence { get; internal set; }        //本场战斗中，所有已执行行动的连续序号。
        public int NormalTurnIndex { get; internal set; }       //正常回合的轮次计数
        public bool ConsumedNormalTurn { get; internal set; }   //本次行动是否消耗正常回合
        public int Turn { get; internal set; }                  //同NormalTurnIndex
        public double StartedAt { get; internal set; }          //行动条上的逻辑开始时间
        public double EndedAt { get; internal set; }            //行动条上的逻辑结束时间，一般和startAt一样
        public BattleUnit Caster { get; internal set; }         //技能释放者
        public SkillConfig Skill { get; internal set; }         //技能
        public BattleUnit MainTarget { get; internal set; }     //主目标
        public int SkillPointsBefore { get; internal set; }     //行动前的战技点
        public int SkillPointsAfter { get; internal set; }      //行动后的战技点
        public int EnergyBefore { get; internal set; }          //能量变化
        public int EnergyAfter { get; internal set; }
        //本次行动真正产生的所有逐条结算结果。
        public IReadOnlyList<BattleEffectRecord> Effects => effects;    
        //根据技能的主要目标规则，第一次解析出来的主目标集合，为相机的视觉效果服务
        public IReadOnlyList<BattleUnit> PrimaryTargets { get; internal set; } = System.Array.Empty<BattleUnit>();
        //记录技能中每一个效果组最终实际解析出的目标集合。
        public IReadOnlyList<BattleResolvedEffectTargets> ResolvedTargets => resolvedTargets;
        internal readonly List<BattleResolvedEffectTargets> resolvedTargets = new();
        internal readonly List<BattleEffectRecord> effects = new();
    }
}
