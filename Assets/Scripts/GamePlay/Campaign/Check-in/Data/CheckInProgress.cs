using System;

namespace Game.Campaign
{
    /// <summary>
    /// 某个玩家在某一期签到活动中的领取进度。
    /// </summary>
    [Serializable]
    public sealed class CheckInProgress
    {
        // 活动唯一标识，用于区分不同期的签到活动。
        // 例如使用 "seven_day_001"
        public string activityId = string.Empty;

        // 已成功领取奖励的次数，七天签到的有效范围为 0～7。
        public int claimedCount = 0;

        // 最后一次成功领取对应的业务日期。
        // 格式：year-Month-day；例:2025-07-02。空字符串表示从未领取。
        public string lastClaimDayKey = string.Empty;

        // 这条进度的数据修订号，每次成功领取后递增。
        public int revision = 0;
    }
}