using UnityEngine;

namespace TowerDefense.Data
{
    public enum AchievementCategory
    {
        All,        // 全部
        Growth,     // 成长
        Battle,     // 战斗
        Collection, // 收集
        Exploration,// 探索
        Fun         // 趣味
    }

    public enum AchievementStatus
    {
        Locked,     // 未解锁
        InProgress, // 进行中
        Claimable,  // 可领取
        Claimed     // 已领取
    }

    [System.Serializable]
    public class AchievementReward
    {
        public RewardType rewardType;
        public int amount;
    }

    [System.Serializable]
    public class AchievementItemData
    {
        public int id;
        public string name;
        public string description;
        public AchievementCategory category;
        public string iconColorHex;        // 图标背景色 (#RRGGBB)
        public string iconSymbol;          // 图标符号 (emoji 或文字)
        public int targetCount;            // 目标数量
        public int currentCount;           // 当前进度
        public AchievementStatus status;
        public AchievementReward[] rewards;
        public int sortIndex;              // 排序权重
    }

    [System.Serializable]
    public class AchievementData
    {
        public AchievementItemData[] items;
        public int[] milestoneThresholds;  // 里程碑节点 (如 [5, 10, 20, 32])
        public AchievementReward[] milestoneRewards; // 里程碑奖励
    }
}
