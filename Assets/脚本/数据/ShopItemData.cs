using UnityEngine;

namespace TowerDefense.Data
{
    public enum ShopCategory
    {
        Resource,   // 资源 - 金币、钻石等
        Hero,       // 英雄
        Tower,      // 防御塔
        Special     // 特惠
    }

    public enum CurrencyType
    {
        Gold,       // 金币
        Diamond,    // 钻石
        Free        // 免费
    }

    [System.Serializable]
    public class ShopItemData
    {
        public int id;
        public string name;
        public string description;
        public ShopCategory category;
        public CurrencyType currencyType;
        public int price;
        public string iconPath;         // Resources 下的图标路径，预留
        public string prefabPath;       // 对应的 Prefab 路径，预留
        public bool isLimited;           // 是否限量
        public int limitCount;           // 限购次数
        public int purchasedCount;       // 已购次数
        public int discount;             // 折扣百分比 (0=无折扣, 50=半价)
        public string tag;              // 自定义标签 (如 "hot", "new", "限时")
        public int rewardGold;           // 购买后获得的金币
        public int rewardDiamond;        // 购买后获得的钻石
        public int rewardStamina;        // 购买后获得的体力
    }

    [System.Serializable]
    public class ShopData
    {
        public ShopItemData[] items;
    }
}
