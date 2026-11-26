namespace Battle.Logic
{
    public enum ActionFailure
    {
        None,
        BattleEnded,
        Resolving,
        NoActor,
        MissingAction,
        InvalidActor,
        NotCurrentActor,
        MissingSkill,
        SkillNotOwned,
        InvalidSkillConfig,
        InsufficientEnergy,
        InsufficientSkillPoints,
        ForeignTarget,
        NoTargets,
        NoLegalAction,
        DriverStopped,
        AlreadyQueued,
        InvalidActionOrigin,
        ResourceReserved,
        InvalidExecutionPolicy,
        QueuedActionCancelled
    }

    /// <summary>只描述查询结果，不持有可以再次执行的许可。</summary>
    public readonly struct ActionValidation
    {
        public ActionFailure Failure { get; }
        public bool IsValid => Failure == ActionFailure.None;
        public ActionValidation(ActionFailure failure) => Failure = failure;

        public string Message => Failure switch
        {
            ActionFailure.None => string.Empty,
            ActionFailure.BattleEnded => "战斗已经结束。",
            ActionFailure.Resolving => "正在结算行动，请勿重复提交。",
            ActionFailure.NoActor => "没有可行动的单位。",
            ActionFailure.MissingAction => "未提交战斗行动。",
            ActionFailure.InvalidActor => "行动者不属于本场战斗或已经倒下。",
            ActionFailure.NotCurrentActor => "尚未轮到该角色行动。",
            ActionFailure.MissingSkill => "未找到要施放的技能。",
            ActionFailure.SkillNotOwned => "该技能不属于当前角色。",
            ActionFailure.InvalidSkillConfig => "技能配置无效。",
            ActionFailure.InsufficientEnergy => "能量不足。",
            ActionFailure.InsufficientSkillPoints => "战技点不足。",
            ActionFailure.ForeignTarget => "目标不属于本场战斗。",
            ActionFailure.NoTargets => "没有合法的技能目标。",
            ActionFailure.NoLegalAction => "自动行动没有可用技能，已停止推进。",
            ActionFailure.DriverStopped => "回合驱动已经停止。",
            ActionFailure.AlreadyQueued => "该行动已经进入插队队列。",
            ActionFailure.InvalidActionOrigin => "技能与行动来源不匹配。",
            ActionFailure.ResourceReserved => "所需资源已被其他待执行行动预留。",
            ActionFailure.InvalidExecutionPolicy => "行动执行策略无效。",
            ActionFailure.QueuedActionCancelled => "插队行动已经取消。",
            _ => "无法执行行动。"
        };
    }

    /// <summary>一次提交的结果；失败时 Resolution 为空，不复用上一次结算。</summary>
    public readonly struct BattleActionResult
    {
        public bool Succeeded { get; }
        public ActionValidation Validation { get; }
        public ActionResolution Resolution { get; }
        public BattleResult Result { get; }

        internal BattleActionResult(ActionValidation validation, BattleResult result,
            ActionResolution resolution = null)
        {
            Validation = validation;
            Result = result;
            Resolution = resolution;
            Succeeded = validation.IsValid && resolution != null;
        }
    }
}
