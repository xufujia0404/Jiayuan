using UnityEngine;

namespace TowerDefense.Data
{
    public enum QuestCategory
    {
        Daily,      // 每日任务
        Main,       // 主线任务
        Achievement,// 成就任务
        Event       // 活动任务
    }

    public enum QuestStatus
    {
        Locked,     // 未解锁
        InProgress, // 进行中
        Claimable,  // 可领取
        Claimed     // 已领取
    }

    public enum RewardType
    {
        Gold,
        Diamond,
        Stamina,
        Item,
        Exp
    }

    [System.Serializable]
    public class QuestReward
    {
        public RewardType rewardType;
        public int amount;
        public string itemPath; // RewardType.Item 时的物品路径
    }

    [System.Serializable]
    public class QuestItemData
    {
        public int id;
        public string name;
        public string description;
        public QuestCategory category;
        public string iconPath;
        public int targetCount;       // 目标数量
        public int currentCount;      // 当前进度
        public QuestStatus status;
        public QuestReward[] rewards;
        public string tag;            // 标签 (如 "hot", "new")
    }

    [System.Serializable]
    public class QuestData
    {
        public QuestItemData[] items;
        public int dailyActivity;     // 今日活跃度
        public int dailyActivityMax;   // 每日活跃度上限
    }
}
