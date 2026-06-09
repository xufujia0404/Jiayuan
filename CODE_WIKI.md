# 塔防游戏 Code Wiki

## 目录
1. [项目概述](#项目概述)
2. [项目结构](#项目结构)
3. [核心架构](#核心架构)
4. [主要模块详解](#主要模块详解)
5. [关键类与函数](#关键类与函数)
6. [数据系统](#数据系统)
7. [事件系统](#事件系统)
8. [项目运行方式](#项目运行方式)

---

## 项目概述

这是一个基于Unity引擎开发的2D塔防游戏项目，类似《王国保卫战》风格。游戏包含完整的核心塔防机制、敌人系统、塔系统、技能系统、存档系统、成就系统等。

### 核心特性
- 多种可升级的防御塔（箭塔、法师塔、兵营、炮塔、剑塔）
- 丰富的敌人类型和属性
- 关卡系统与波次管理
- 玩家等级系统与经验值
- 成就系统与商城系统
- 技能系统（闪电、冰冻、治疗、陨石等）
- 完整的存档系统

---

## 项目结构

### 文件夹结构
```
Assets/
├── AchievementUI/          # 成就UI资源
├── Animations/             # 动画资源
├── Art/                    # 美术资源
│   ├── Effects/            # 特效
│   ├── Soldiers/           # 士兵
│   ├── Tiles/              # 地图瓦片
│   ├── Towers/             # 塔
│   └── UI/                 # UI资源
├── Audio/                  # 音频资源
├── BluBlu Games/           # 第三方资源
├── Codely/                 # Codely工具输出
├── Editor/                 # 编辑器工具
├── Layer Lab/              # 第三方素材
├── Prefabs/                # 预制体
│   ├── Enemies/            # 敌人预制体
│   ├── Projectiles/        # 投射物
│   ├── Skills/             # 技能
│   ├── Soldiers/           # 士兵
│   ├── Tiles/              # 瓦片
│   ├── Towers/             # 塔
│   └── UI/                 # UI
├── Resources/              # 资源目录
│   └── Data/               # 数据资产
├── Scenes/                 # 场景
└── Scripts/                # 脚本代码
    ├── Core/               # 核心系统
    ├── Data/               # 数据类
    ├── Enemy/              # 敌人系统
    ├── Hero/               # 英雄系统
    ├── Map/                # 地图系统
    ├── Save/               # 存档系统
    ├── Skill/              # 技能系统
    ├── Skills/             # 技能实现
    ├── Tower/              # 塔系统
    ├── UI/                 # UI系统
    ├── Utils/              # 工具类
    └── Wave/               # 波次系统
```

---

## 核心架构

### 架构设计模式
项目采用**单例模式** + **事件驱动架构**：
- 所有核心管理类使用单例模式继承自 `Singleton<T>`
- 模块间通信通过 `EventBus` 事件总线进行
- 数据使用ScriptableObject进行配置管理

### 核心循环流程
1. 游戏初始化 → `GameManager` 启动
2. 加载关卡 → `LevelBuilder` 构建地图
3. 开始波次 → `WaveManager` 控制波次流程
4. 生成敌人 → `EnemySpawner` 按配置生成敌人
5. 防御塔攻击 → `Tower` 自动寻路并攻击
6. 玩家操作 → UI交互、建造、升级、出售塔
7. 胜利/失败判定 → 检查生命/波次完成状态

---

## 主要模块详解

### 1. 核心系统 (Core)

#### 1.1 GameManager - 游戏主控制器
**位置**: [GameManager.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Core/GameManager.cs)

**职责**:
- 管理游戏状态（菜单、游戏中、暂停、结束、胜利）
- 控制关卡加载与游戏流程
- 管理金币与生命值
- 提供游戏统计（敌人击杀数、游戏时间等）
- 处理调试功能

**核心属性**:
- `GameState CurrentState` - 当前游戏状态
- `int CurrentGold` - 当前金币数
- `int CurrentLife` - 当前生命值
- `int CurrentWave` - 当前波次
- `float GameSpeed` - 游戏速度
- `bool IsPlaying` - 是否正在游戏中

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `LoadLevel(int levelIndex)` | 加载指定关卡 |
| `StartGame(int totalWaves)` | 开始游戏 |
| `PauseGame()` / `ResumeGame()` | 暂停/继续游戏 |
| `GameOver(bool isVictory)` | 游戏结束 |
| `AddGold(int amount)` | 增加金币 |
| `SpendGold(int amount)` | 花费金币 |
| `TakeDamage(int damage)` | 受到伤害 |

**游戏状态枚举**:
```csharp
public enum GameState
{
    Menu,       // 菜单
    Playing,    // 游戏中
    Paused,     // 暂停
    GameOver,   // 游戏结束
    Victory     // 胜利
}
```

---

#### 1.2 EventBus - 事件总线
**位置**: [EventBus.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Core/EventBus.cs)

**职责**:
- 提供全局事件订阅、取消订阅和发布功能
- 解耦各个系统，实现模块间通信

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `Subscribe<T>(Action<T> callback)` | 订阅事件 |
| `Unsubscribe<T>(Action<T> callback)` | 取消订阅 |
| `Publish<T>(T eventData)` | 发布事件 |
| `Clear()` | 清空所有事件 |

**预定义事件结构体**:
| 事件名 | 说明 |
|--------|------|
| `GameStartEvent` | 游戏开始 |
| `GamePauseEvent` | 游戏暂停 |
| `GameOverEvent` | 游戏结束 |
| `WaveStartEvent` | 波次开始 |
| `WaveEndEvent` | 波次结束 |
| `EnemySpawnEvent` | 敌人生成 |
| `EnemyDeathEvent` | 敌人死亡 |
| `EnemyReachEndEvent` | 敌人到达终点 |
| `TowerPlacedEvent` | 塔被放置 |
| `TowerUpgradedEvent` | 塔被升级 |
| `TowerSoldEvent` | 塔被出售 |
| `GoldChangedEvent` | 金币变化 |
| `LifeChangedEvent` | 生命变化 |
| `SkillUsedEvent` | 技能使用 |
| `ExpChangedEvent` | 经验值变化 |
| `LevelUpEvent` | 玩家升级 |

**使用示例**:
```csharp
// 订阅事件
EventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);

// 发布事件
EventBus.Publish(new GoldChangedEvent { CurrentGold = 100, Change = 50 });

// 取消订阅
EventBus.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
```

---

#### 1.3 Singleton - 单例基类
**位置**: [Singleton.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Core/Singleton.cs)

**职责**:
- 提供泛型单例基类实现
- 确保场景中只有一个实例
- 自动处理单例的生命周期

**使用方式**:
```csharp
public class MyManager : Singleton<MyManager>
{
    protected override void Awake()
    {
        base.Awake();
        // 初始化代码
    }
}
```

---

#### 1.4 PlayerWallet - 玩家钱包
**位置**: [PlayerWallet.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/PlayerWallet.cs)

**职责**:
- 管理玩家的多种货币（金币、钻石、体力）
- 提供货币的增加、消费、查询功能

**核心属性**:
- `int Gold` - 金币
- `int Diamond` - 钻石
- `int Stamina` - 体力

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `AddGold(int amount)` | 增加金币 |
| `SpendGold(int amount)` | 花费金币 |
| `HasEnoughGold(int amount)` | 检查金币是否足够 |
| `AddDiamond(int amount)` | 增加钻石 |
| `AddStamina(int amount)` | 增加体力 |

---

#### 1.5 PlayerLevelSystem - 玩家等级系统
**位置**: [PlayerLevelSystem.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Core/PlayerLevelSystem.cs)

**职责**:
- 管理玩家等级与经验值
- 自动处理升级逻辑
- 计算升级所需经验值
- 与存档系统集成

**核心属性**:
- `int Level` - 当前等级
- `int CurrentExp` - 当前经验值
- `int ExpToNextLevel` - 到下一级所需经验值
- `float ExpProgress` - 经验进度（0-1）
- `bool IsMaxLevel` - 是否达到最高等级

**经验计算公式**:
```
升级所需经验 = 基础经验 + (当前等级 - 1) * 经验增长值
默认: 100 + (level - 1) * 50
```

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `AddExp(int amount)` | 增加经验值 |
| `GetExpRequiredForLevel(int level)` | 获取指定等级所需经验值 |
| `SetLevelData(int level, int exp)` | 强制设置等级和经验值 |
| `PersistToSave()` | 保存到存档 |

---

### 2. 地图系统 (Map)

#### 2.1 LevelBuilder - 关卡构建器
**位置**: [LevelBuilder.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Map/LevelBuilder.cs)

**职责**:
- 根据 `LevelData` 构建地图
- 自动选择合适的路径瓦片
- 生成塔的建造位置
- 管理地图的清理和重建

**地图字符编码**:
| 字符 | 含义 |
|------|------|
| `G` 或 `.` | 草地/空地 |
| `P` | 路径 |
| `B` | 建塔位 |
| `D` | 装饰 |
| `S` | 起点 |
| `E` | 终点 |

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `BuildLevel(LevelData levelData)` | 构建指定关卡 |
| `ClearAll()` | 清空地图 |
| `GetPathWaypoints()` | 获取路径点列表 |

---

#### 2.2 MapGenerator - 地图生成器
**位置**: [MapGenerator.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Map/MapGenerator.cs)

**职责**:
- 从 Tilemap 读取并初始化地图
- 生成塔槽
- 初始化路径

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `InitializeMap()` | 初始化地图 |
| `GetTowerSlots()` | 获取塔槽列表 |
| `ClearMap()` | 清空地图 |

---

### 3. 敌人系统 (Enemy)

#### 3.1 Enemy - 敌人主体类
**位置**: [Enemy.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Enemy/Enemy.cs)

**职责**:
- 敌人的主要控制器
- 管理敌人的生命值、移动、状态
- 处理血条显示
- 与 GameManager 交互（给予金币、造成伤害等）

**核心组件依赖**:
- `EnemyMovement` - 移动组件
- `EnemyHealth` - 生命值组件
- `EnemyAnimation` - 动画组件

**核心属性**:
- `EnemyData Data` - 敌人数据配置
- `bool IsDead` - 是否死亡
- `EnemyMovement Movement` - 移动组件
- `EnemyHealth Health` - 生命值组件

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `Initialize(EnemyData data)` | 初始化敌人 |
| `StartMovement(PathCreator pathCreator)` | 开始沿路径移动 |
| `OnReachedEnd()` | 到达终点时调用 |
| `OnDeath()` | 死亡时调用 |
| `TakeDamage(int damage)` | 受到伤害 |
| `TakeDamage(int damage, DamageType damageType)` | 受到特定类型伤害 |
| `ApplySlow(float slowFactor, float duration)` | 施加减速效果 |
| `ApplyStun(float duration)` | 施加眩晕效果 |

---

#### 3.2 EnemySpawner - 敌人生成器
**位置**: [EnemySpawner.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Enemy/EnemySpawner.cs)

**职责**:
- 按波次配置生成敌人
- 管理敌人生成间隔
- 追踪波次中的敌人数量
- 提供敌人查询功能

**核心属性**:
- `static int CurrentWaveId` - 当前波次ID
- `static int TotalEnemiesInWave` - 波次总敌人数量
- `static int RemainingEnemies` - 剩余敌人数量
- `bool IsSpawning` - 是否正在生成

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `StartWave(WaveData waveData)` | 开始波次 |
| `StopSpawning()` | 停止生成 |
| `GetClosestEnemy(Vector3 position, float maxDistance)` | 获取最近的敌人 |
| `GetEnemiesInRange(Vector3 position, float range)` | 获取范围内的敌人 |
| `static EnemyBorn(GameObject enemy)` | 记录敌人生成 |
| `static OnEnemyKilled(GameObject enemy, int enemyWaveId)` | 记录敌人死亡 |

**波次追踪机制**:
使用 HashSet 进行去重，确保每个敌人只统计一次死亡，解决重复计数问题。

---

#### 3.3 EnemyHealth - 敌人生命值组件
**位置**: [EnemyHealth.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Enemy/EnemyHealth.cs)

**职责**:
- 管理敌人当前生命值
- 处理伤害计算与减伤
- 触发死亡事件

---

#### 3.4 EnemyMovement - 敌人移动组件
**位置**: [EnemyMovement.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Enemy/EnemyMovement.cs)

**职责**:
- 沿路径移动敌人
- 处理减速与眩晕效果
- 计算移动进度

---

#### 3.5 DamageType - 伤害类型枚举
**位置**: [DamageType.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Enemy/DamageType.cs)

```csharp
public enum DamageType
{
    Physical,   // 物理伤害
    Magic,      // 魔法伤害
    Explosion   // 爆炸伤害
}
```

---

### 4. 塔系统 (Tower)

#### 4.1 Tower - 塔主体类
**位置**: [Tower.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/Tower.cs)

**职责**:
- 塔的主要控制器
- 自动寻找并锁定目标
- 管理塔的升级与出售
- 显示攻击范围

**塔状态枚举**:
```csharp
public enum TowerState
{
    Idle,        // 空闲
    Targeting,   // 锁定目标中
    Attacking,   // 攻击中
    Upgrading,   // 升级中
    Selling      // 出售中
}
```

**核心属性**:
- `TowerData Data` - 塔数据配置
- `int CurrentLevel` - 当前等级
- `TowerState State` - 当前状态
- `Enemy CurrentTarget` - 当前目标
- `TowerData.TowerStats CurrentStats` - 当前属性
- `bool IsMaxLevel` - 是否达到最高等级

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `Initialize(TowerData data, TowerSlot slot)` | 初始化塔 |
| `Upgrade()` | 升级塔 |
| `Sell()` | 出售塔 |
| `GetUpgradeCost()` | 获取升级费用 |
| `GetSellValue()` | 获取出售价值 |
| `ShowRange()` / `HideRange()` | 显示/隐藏攻击范围 |

**目标选择策略**:
优先选择离终点最近的敌人（移动进度最高的敌人）。

---

#### 4.2 TowerSlot - 塔槽类
**位置**: [TowerSlot.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/TowerSlot.cs)

**职责**:
- 管理一个可放置塔的位置
- 处理塔的放置、移除
- 处理玩家交互（点击等）

**核心属性**:
- `Tower CurrentTower` - 当前放置的塔
- `bool HasTower` - 是否有塔

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `PlaceTower(TowerData towerData)` | 放置塔 |
| `RemoveTower()` | 移除塔 |

---

#### 4.3 TowerAttack - 塔攻击组件
**位置**: [TowerAttack.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/TowerAttack.cs)

**职责**:
- 处理塔的攻击逻辑
- 生成投射物
- 应用伤害

---

#### 4.4 BarracksAttack - 兵营攻击组件
**位置**: [BarracksAttack.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/BarracksAttack.cs)

**职责**:
- 兵营专用的攻击逻辑
- 生成士兵进行近战拦截

---

#### 4.5 Soldier - 士兵类
**位置**: [Soldier.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/Soldier.cs)

**职责**:
- 兵营塔生成的士兵
- 与敌人近战战斗
- 阻挡敌人前进

---

#### 4.6 Projectile - 投射物类
**位置**: [Projectile.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Tower/Projectile.cs)

**职责**:
- 投射物的移动
- 碰撞检测与伤害
- 特效与音效

---

### 5. 波次系统 (Wave)

#### 5.1 WaveManager - 波次管理器
**位置**: [WaveManager.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Wave/WaveManager.cs)

**职责**:
- 管理整个游戏的波次流程
- 控制波次间的倒计时
- 处理波次开始与结束
- 更新波次UI

**核心属性**:
- `int CurrentWave` - 当前波次
- `int TotalWaves` - 总波次数
- `bool IsWaveActive` - 波次是否激活

**核心方法**:
| 方法名 | 说明 |
|--------|------|
| `StartNextWave()` | 开始下一波 |
| `OnWaveComplete()` | 波次完成时调用 |
| `SkipToNextWave()` | 跳至下一波 |
| `ForceStartWave()` | 强制开始波次 |
| `Reset()` | 重置波次 |

**波次流程**:
1. 波次过渡阶段 → 显示倒计时
2. 波次开始 → 生成敌人
3. 波次进行 → 敌人移动、塔攻击
4. 波次结束 → 所有敌人被消灭或到达终点
5. 回到过渡阶段或游戏胜利

---

### 6. 数据系统 (Data)

#### 6.1 LevelData - 关卡数据
**位置**: [LevelData.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/LevelData.cs)

**职责**:
- 存储关卡的所有配置数据
- 使用ScriptableObject，可在Inspector中编辑

**核心属性**:
- `string levelName` - 关卡名称
- `string sceneName` - 场景名称
- `string[] mapRows` - 地图字符数组
- `List<WaveData> waves` - 波次数据列表
- `int waveCount` - 波次数
- `int initialGold` - 初始金币
- `int initialLife` - 初始生命

---

#### 6.2 WaveData - 波次数据
**位置**: [WaveData.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/WaveData.cs)

**职责**:
- 存储一个波次的敌人配置
- 支持多种敌人混合

**核心属性**:
- `int waveNumber` - 波次数
- `float waveDelay` - 波次延迟
- `float spawnInterval` - 生成间隔
- `List<WaveEnemy> enemies` - 敌人列表

**WaveEnemy结构**:
```csharp
public struct WaveEnemy
{
    public EnemyData enemyData;   // 敌人数据
    public int count;             // 数量
    public float spawnDelay;      // 生成延迟
    public string spawnPoint;     // 生成点
}
```

---

#### 6.3 EnemyData - 敌人数据
**位置**: [EnemyData.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/EnemyData.cs)

**职责**:
- 存储敌人的所有属性配置
- 可在Inspector中编辑

**核心属性**:
- `string enemyName` - 敌人名称
- `EnemyType enemyType` - 敌人类型
- `GameObject enemyPrefab` - 敌人预制体
- `EnemyStats stats` - 敌人属性

**EnemyStats结构**:
```csharp
public struct EnemyStats
{
    // 基础属性
    public int maxHealth;           // 最大生命值
    public float moveSpeed;         // 移动速度
    public int goldReward;          // 金币奖励
    public int lifeDamage;          // 对玩家造成的伤害
    public int attackDamage;        // 对士兵造成的伤害
    
    // 抗性
    public float physicalResistance;   // 物理抗性
    public float magicResistance;      // 魔法抗性
    public float explosionResistance;  // 爆炸抗性
    
    // 特殊属性
    public bool isFlying;           // 是否飞行
    public bool isArmored;          // 是否装甲
    public bool isMagicImmune;      // 是否魔法免疫
    public bool canBeSlowed;        // 是否可被减速
    public float slowImmunity;      // 减速免疫度
    
    // Boss属性
    public bool isBoss;             // 是否Boss
    public float bossHealthMultiplier;  // Boss血量倍率
    public string bossName;         // Boss名称
}
```

**EnemyType枚举**:
```csharp
public enum EnemyType
{
    Goblin,     // 哥布林
    Orc,        // 兽人
    Troll,      // 巨魔
    Demon,      // 恶魔
    Spider,     // 蜘蛛
    Flying,     // 飞行
    Armored,    // 装甲
    Fast,       // 快速
    Boss,       // Boss
    MiniBoss    // 小Boss
}
```

---

#### 6.4 TowerData - 塔数据
**位置**: [TowerData.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/TowerData.cs)

**职责**:
- 存储塔的所有属性配置
- 支持多级升级
- 可在Inspector中编辑

**核心属性**:
- `string towerName` - 塔名称
- `string description` - 描述
- `TowerType towerType` - 塔类型
- `GameObject towerPrefab` - 塔预制体
- `GameObject projectilePrefab` - 投射物预制体
- `Sprite icon` - 图标
- `TowerStats[] levels` - 各等级属性
- `SoldierData soldierData` - 士兵数据（仅兵营）

**TowerStats结构**:
```csharp
public struct TowerStats
{
    public int level;               // 等级
    public int cost;                // 建造费用
    public int upgradeCost;         // 升级费用
    public int sellValue;           // 出售价值
    public float damage;            // 伤害
    public float attackRange;       // 攻击范围
    public float attackSpeed;       // 攻击速度
    public ProjectileType projectileType;  // 投射物类型
    public int projectileCount;     // 投射物数量
    public float splashRadius;      // 溅射范围
    public float slowAmount;        // 减速幅度
    public float slowDuration;      // 减速持续时间
    public int maxTargets;          // 最大目标数
    public int pierceCount;         // 穿透数量
}
```

**TowerType枚举**:
```csharp
public enum TowerType
{
    Archer,     // 箭塔
    Mage,       // 法师塔
    Barracks,   // 兵营
    Artillery,  // 炮塔
    Sword       // 剑塔
}
```

**ProjectileType枚举**:
```csharp
public enum ProjectileType
{
    Arrow,      // 箭矢
    Magic,      // 魔法
    Cannonball, // 炮弹
    None        // 无（近战）
}
```

---

#### 6.5 GameConfig - 游戏配置（旧版）
**位置**: [GameConfig.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Data/Config/GameConfig.cs)

**说明**: 这是早期的配置系统，现在主要使用各个独立的ScriptableObject（TowerData、EnemyData等）。

---

### 7. 技能系统 (Skill/Skills)

#### 7.1 SkillBase - 技能基类
**位置**: [SkillBase.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skill/SkillBase.cs)

**职责**:
- 提供技能的基础接口
- 所有技能继承此类

---

#### 7.2 具体技能实现

| 技能 | 位置 | 效果 |
|------|------|------|
| FreezeSkill | [FreezeSkill.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skill/FreezeSkill.cs) | 冰冻敌人 |
| HealSkill | [HealSkill.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skill/HealSkill.cs) | 治疗玩家生命值 |
| MeteorSkill | [MeteorSkill.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skill/MeteorSkill.cs) | 召唤陨石造成范围伤害 |
| LightningBolt | [LightningBolt.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skills/LightningBolt.cs) | 闪电技能 |
| LightningSkillController | [LightningSkillController.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Skills/LightningSkillController.cs) | 闪电技能控制器 |

---

### 8. 存档系统 (Save)

#### 8.1 SaveSystem - 存档系统
**位置**: [SaveSystem.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Save/SaveSystem.cs)

**职责**:
- 管理游戏存档的保存与加载
- 使用JSON序列化
- 处理存档文件的读写

---

#### 8.2 GameSaveData - 存档数据
**位置**: [GameSaveData.cs](file:///c:/Users/97809/Desktop/sttop5/Assets/Scripts/Save/GameSaveData.cs)

**职责**:
- 定义存档数据结构
- 包含玩家数据、关卡进度、成就等

---

### 9. UI系统 (UI)

主要UI类:
- `GameUI` - 游戏主UI
- `MainMenuUI` - 主菜单UI
- `TowerSelectPanel` - 塔选择面板
- `TowerInfoPanel` - 塔信息面板
- `ShopPanel` - 商城面板
- `AchievementPanel` - 成就面板
- `QuestPanel` - 任务面板
- `VictoryPanel` - 胜利面板
- `BuildButtonManager` - 建造按钮管理器
- 等等...

---

## 关键类与函数

### 核心启动流程
```
1. MainMenu场景加载
2. 玩家选择关卡
3. GameScene加载
4. GameManager.Awake() → InitializeGame()
5. GameManager.Start() → LoadLevel(0)
6. LevelBuilder.BuildLevel() → 构建地图
7. MapGenerator.InitializeMap() → 生成塔槽
8. WaveManager.Start() → 等待GameStartEvent
9. GameManager.StartGame() → 发布GameStartEvent
10. WaveManager.OnGameStart() → ForceStartWave()
11. EnemySpawner.StartWave() → 开始生成敌人
```

### 敌人生命周期
```
1. EnemySpawner.SpawnEnemy() → Instantiate
2. Enemy.Initialize() → 设置数据和组件
3. Enemy.StartMovement() → 开始沿路径移动
4. Enemy.Update() → 更新血条位置
5. 被塔攻击 → EnemyHealth.TakeDamage()
6. 血量归0 → Enemy.OnDeath() → 给金币 + 统计击杀
   或到达终点 → Enemy.OnReachedEnd() → 扣除生命
7. Destroy(gameObject)
```

### 塔生命周期
```
1. 玩家点击塔槽 → TowerSlot.PlaceTower()
2. Instantiate塔预制体 → Tower.Initialize()
3. Tower.Update() → 每帧寻找目标
4. 发现目标 → RotateTowardsTarget() + Attack()
5. TowerAttack.Attack() → 生成投射物
6. 投射物碰撞 → 造成伤害
7. 玩家点击升级 → Tower.Upgrade()
   或点击出售 → Tower.Sell()
8. Destroy(gameObject)
```

---

## 数据系统

### ScriptableObject配置资产
项目使用ScriptableObject存储配置数据，存放于 `Assets/Resources/Data/`:
- `TowerData` - 塔配置
- `EnemyData` - 敌人配置
- `LevelData` - 关卡配置
- `WaveData` - 波次配置
- `GameConfig` - 游戏配置（旧版）
- `Achievements.json` - 成就数据
- `ShopItems.json` - 商店物品
- `QuestItems.json` - 任务物品

### 创建新配置资产
在Unity编辑器中:
1. 右键点击 Project 窗口
2. 选择 `TowerDefense/[DataType]`
3. 在Inspector中编辑属性

---

## 事件系统

### 事件使用最佳实践
1. **订阅时机**: 在 `Start()` 或 `OnEnable()` 中订阅
2. **取消订阅**: 在 `OnDestroy()` 或 `OnDisable()` 中取消订阅，防止内存泄漏
3. **事件数据**: 使用struct定义事件数据，避免GC
4. **事件命名**: 以 `Event` 结尾，清晰表达含义

### 示例: 订阅和取消订阅
```csharp
private void OnEnable()
{
    EventBus.Subscribe<EnemyDeathEvent>(OnEnemyDeath);
}

private void OnDisable()
{
    EventBus.Unsubscribe<EnemyDeathEvent>(OnEnemyDeath);
}

private void OnEnemyDeath(EnemyDeathEvent e)
{
    Debug.Log($"Enemy died: {e.Enemy.name}, Reward: {e.Reward}");
}
```

---

## 项目运行方式

### 环境要求
- Unity 2021.3 LTS 或更高版本
- 推荐使用Unity Hub管理版本

### 运行步骤
1. 打开Unity Hub，点击 "Add" 添加项目
2. 选择项目根目录 `sttop5`
3. 等待Unity导入资源
4. 打开 `Assets/Scenes/MainMenu.scene`
5. 点击 Play 按钮运行游戏

### 关卡测试
1. 打开 `Assets/Scenes/GameScene.scene`
2. 在Hierarchy中选择 `GameManager`
3. 在Inspector中配置关卡数据
4. 点击 Play 按钮

### 调试模式
在 `GameManager` 中开启 `Debug Mode` 后:
- 按 `F1`: 增加100金币
- 按 `F2`: 恢复生命值
- 按 `F3`: 调整游戏速度

### 编辑器工具
项目包含自定义编辑器工具:
- `BuildAchievementUI` - 成就UI构建工具
- `KRRestyleHUD` - HUD样式工具
- `SoldierAnimatorSetup` - 士兵动画设置工具
- `UILayoutBuilder` - UI布局构建工具
- `VictoryPanelBuilder` - 胜利面板构建工具

---

## 依赖关系图

```
GameManager (核心控制器)
├── Singleton<GameManager>
├── EventBus (事件通信)
├── PlayerWallet (货币)
├── PlayerLevelSystem (等级)
├── WaveManager (波次)
│   └── EnemySpawner (生成敌人)
│       └── Enemy (敌人)
│           ├── EnemyMovement
│           ├── EnemyHealth
│           └── EnemyAnimation
├── LevelBuilder (地图)
│   └── MapGenerator
└── Tower (塔)
    ├── TowerAttack (攻击)
    ├── Projectile (投射物)
    └── BarracksAttack / Soldier (兵营)

SaveSystem (存档)
└── GameSaveData

UI系统 (多个UI类)
└── EventBus (事件更新)
```

---

## 扩展开发指南

### 添加新塔类型
1. 在 `TowerType` 枚举中添加新类型
2. 创建新的塔预制体
3. 创建 `TowerData` 配置资产
4. 实现塔的特殊逻辑（如需要，继承 `Tower` 或 `TowerAttack`）
5. 在UI中添加新塔的按钮

### 添加新敌人类型
1. 在 `EnemyType` 枚举中添加新类型
2. 创建新的敌人预制体
3. 创建 `EnemyData` 配置资产
4. 在波次配置中添加新敌人

### 添加新技能
1. 创建新技能类继承 `SkillBase`
2. 实现技能逻辑
3. 在UI中添加技能按钮
4. 添加技能冷却、消耗等管理

---

## 常见问题

### Q: 如何创建新关卡？
A: 
1. 创建新的 `LevelData` 资产
2. 编辑 `mapRows` 字符数组定义地图
3. 配置波次数据 `waves`
4. 在 `GameManager` 中添加新关卡

### Q: 存档文件在哪里？
A: 
- Windows: `%userprofile%\AppData\LocalLow\[CompanyName]\[ProductName]\`
- macOS: `~/Library/Application Support/[CompanyName]/[ProductName]/`

### Q: 如何调整游戏平衡性？
A: 编辑对应的ScriptableObject配置资产:
- 塔属性 → `TowerData`
- 敌人属性 → `EnemyData`
- 波次配置 → `WaveData` / `LevelData`
- 初始资源 → `LevelData`

---

## 更新日志

### 当前版本
- 完整的塔防核心玩法
- 5种塔类型（箭塔、法师塔、兵营、炮塔、剑塔）
- 多种敌人类型
- 技能系统
- 成就系统
- 商城系统
- 存档系统

---

*本文档最后更新: 2026年6月*
