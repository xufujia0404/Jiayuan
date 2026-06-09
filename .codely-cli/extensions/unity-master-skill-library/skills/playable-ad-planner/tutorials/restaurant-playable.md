# Unity 试玩广告教程 - 餐厅模拟

> 本教程演示如何快速制作一个30秒餐厅模拟试玩广告

---

## 执行规则（必须遵守）

1. **严格按顺序执行**：从步骤 1 到步骤 8，不跳步
2. **每步完成后验证**：执行完一步，立即检查结果
3. **优先使用现有资产**：检查项目中是否已有可复用的资产
4. **遇错即停**：如果某步失败 2 次，停止并报告问题

---

## 试玩流程设计

```
开始页 (2秒)
    ↓ 点击开始
游戏玩法 (25秒)
    ├── 引导：接单 (3秒)
    ├── 引导：制作食物 (5秒)
    ├── 引导：打包 (5秒)
    └── 自由服务顾客 (12秒)
结算页面 (3秒)
    └── 显示收入，引导重玩
```

---

## 开发顺序总览

```
步骤 1：验证项目资源
步骤 2：创建开始页
步骤 3：创建引导系统
步骤 4：实现订单系统
步骤 5：实现食物制作
步骤 6：实现打包服务
步骤 7：创建结算页面
步骤 8：最终验证
```

---

## 步骤 1：验证项目资源

### 1.1 检查现有资产

**操作**：使用以下路径检查项目中已有的资产

```
必须检查的路径：
□ Assets/Scenes/
□ Assets/Prefabs/
□ Assets/Models/ (食物、设备等)
□ Assets/Sprites/
□ Assets/Scripts/
□ Assets/Resources/
```

### 1.2 记录可复用资产

**示例**：
```
【已存在可复用】
- ✓ Assets/Prefabs/Food/Pizza.prefab - 披萨模型
- ✓ Assets/Prefabs/Equipment/Oven.prefab - 烤炉
- ✓ Assets/Scripts/PlayerController.cs - 玩家控制

【缺失需创建】
- ✗ MainMenuManager.cs
- ✗ GuideManager.cs
- ✗ OrderManager.cs
```

**验证标准**：
- ✓ 已盘点所有现有资产
- ✓ 已标记可复用和需创建的资产

---

## 步骤 2：创建开始页

### 2.1 创建开始页场景

**操作**：
1. 创建新场景：`Assets/Scenes/MainMenu.unity`
2. 设置 Camera：
   - Position: (0, 0, -10)
   - Clear Flags: Solid Color
   - Background: #1A1A2E

### 2.2 创建 UI

**操作**：
1. 创建 Canvas（Screen Space - Overlay）
2. 创建 Title Text：
   - Text: "Pizza Restaurant"
   - Font Size: 72
   - Color: White
   - Position: 屏幕中央上方

3. 创建 Start Button：
   - Text: "TAP TO START"
   - Font Size: 36
   - Position: 屏幕中央下方

### 2.3 创建 MainMenuManager.cs

**路径**：`Assets/Scripts/UI/MainMenuManager.cs`

**代码**：
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button startButton;
    [SerializeField] private Image screenFader;
    [SerializeField] private AudioClip tapSound;
    
    private void Start()
    {
        // 初始化
        screenFader.gameObject.SetActive(true);
        screenFader.color = new Color(0, 0, 0, 1);
        
        // 淡入效果
        screenFader.DOFade(0f, 0.5f).OnComplete(() => {
            screenFader.gameObject.SetActive(false);
        });
        
        // 开始按钮动画（吸引点击）
        startButton.transform.DOScale(1.1f, 0.5f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
        
        // 绑定点击事件
        startButton.onClick.AddListener(OnStartButtonClicked);
    }
    
    private void OnStartButtonClicked()
    {
        // 停止动画
        DOTween.Kill(startButton.transform);
        
        // 播放音效
        if (tapSound != null)
            AudioSource.PlayClipAtPoint(tapSound, Camera.main.transform.position);
        
        // 淡出并进入游戏
        screenFader.gameObject.SetActive(true);
        screenFader.DOFade(1f, 0.3f).OnComplete(() => {
            SceneManager.LoadScene("GameScene");
        });
    }
}
```

**验证标准**：
- ✓ 开始页显示标题和按钮
- ✓ 按钮有脉冲动画
- ✓ 点击按钮可切换到游戏场景

---

## 步骤 3：创建引导系统

### 3.1 创建 GuideManager.cs

**路径**：`Assets/Scripts/Core/GuideManager.cs`

**代码**：
```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance { get; private set; }
    
    [Header("Guide Settings")]
    [SerializeField] private List<Transform> guideTargets;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float arrowOffset = 2f;
    
    private int currentIndex = 0;
    private GameObject currentArrow;
    private bool isGuideActive = true;
    
    public bool IsGuideActive => isGuideActive;
    public int CurrentStep => currentIndex;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (guideTargets.Count > 0)
        {
            ShowArrow(guideTargets[0]);
        }
    }
    
    private void Update()
    {
        if (!isGuideActive || currentArrow == null) return;
        
        // 箭头指向当前目标
        UpdateArrowPosition();
    }
    
    private void ShowArrow(Transform target)
    {
        if (currentArrow != null)
            Destroy(currentArrow);
        
        currentArrow = Instantiate(arrowPrefab);
        currentArrow.transform.SetParent(GameObject.Find("Canvas").transform, false);
        
        UpdateArrowPosition();
    }
    
    private void UpdateArrowPosition()
    {
        if (currentIndex >= guideTargets.Count) return;
        
        Transform target = guideTargets[currentIndex];
        Vector3 screenPos = Camera.main.WorldToScreenPoint(target.position + Vector3.up * arrowOffset);
        currentArrow.transform.position = screenPos;
        
        // 箭头上下浮动动画
        currentArrow.transform.GetChild(0).localPosition = Vector3.up * Mathf.Sin(Time.time * 3f) * 0.3f;
    }
    
    public void CompleteCurrentStep()
    {
        currentIndex++;
        
        if (currentIndex < guideTargets.Count)
        {
            ShowArrow(guideTargets[currentIndex]);
        }
        else
        {
            // 引导完成
            CompleteGuide();
        }
    }
    
    private void CompleteGuide()
    {
        isGuideActive = false;
        if (currentArrow != null)
            Destroy(currentArrow);
        
        Debug.Log("[GuideManager] 引导完成，进入自由游戏");
    }
}
```

### 3.2 创建引导箭头 Prefab

**操作**：
1. 创建 UI Image 作为箭头
2. 使用简单的三角形或手指图标
3. 添加上下浮动动画

**验证标准**：
- ✓ 引导箭头正确显示
- ✓ 箭头指向引导目标
- ✓ 完成步骤后箭头移动到下一目标

---

## 步骤 4：实现订单系统

### 4.1 创建 OrderManager.cs

**路径**：`Assets/Scripts/Game/OrderManager.cs`

**代码**：
```csharp
using UnityEngine;
using System.Collections.Generic;

public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance { get; private set; }
    
    [Header("Order Settings")]
    [SerializeField] private List<OrderConfig> possibleOrders;
    [SerializeField] private int maxActiveOrders = 3;
    [SerializeField] private Transform orderUIContainer;
    
    private List<Order> activeOrders = new List<Order>();
    private int completedOrders = 0;
    
    public int CompletedOrders => completedOrders;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    public void GenerateNewOrder()
    {
        if (activeOrders.Count >= maxActiveOrders) return;
        
        OrderConfig config = possibleOrders[Random.Range(0, possibleOrders.Count)];
        Order newOrder = new Order(config);
        activeOrders.Add(newOrder);
        
        // 更新 UI 显示
        UpdateOrderUI();
    }
    
    public bool DeliverOrder(FoodType foodType)
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            if (activeOrders[i].Config.foodType == foodType)
            {
                completedOrders++;
                int reward = activeOrders[i].Config.reward;
                activeOrders.RemoveAt(i);
                
                // 加分
                if (GameManager.Instance != null)
                    GameManager.Instance.AddScore(reward);
                
                // 引导完成检测
                if (GuideManager.Instance != null && GuideManager.Instance.IsGuideActive)
                    GuideManager.Instance.CompleteCurrentStep();
                
                UpdateOrderUI();
                return true;
            }
        }
        return false;
    }
    
    private void UpdateOrderUI()
    {
        // 更新订单 UI 显示
        // 实现根据项目 UI 结构调整
    }
}

[System.Serializable]
public class OrderConfig
{
    public FoodType foodType;
    public string orderName;
    public int reward = 10;
    public float timeLimit = 30f;
}

public class Order
{
    public OrderConfig Config { get; private set; }
    public float RemainingTime { get; private set; }
    
    public Order(OrderConfig config)
    {
        Config = config;
        RemainingTime = config.timeLimit;
    }
}

public enum FoodType
{
    Pizza,
    Burger,
    Drink
}
```

**验证标准**：
- ✓ 可以生成新订单
- ✓ 可以完成订单并获得分数

---

## 步骤 5：实现食物制作

### 5.1 创建 FoodMachine.cs

**路径**：`Assets/Scripts/Game/FoodMachine.cs`

**代码**：
```csharp
using UnityEngine;

public class FoodMachine : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private FoodType producesFood;
    [SerializeField] private float productionTime = 3f;
    [SerializeField] private Transform spawnPoint;
    
    private bool isProducing = false;
    private float productionProgress = 0f;
    
    public FoodType ProducesFood => producesFood;
    
    public void Interact()
    {
        if (!isProducing)
        {
            StartProduction();
        }
    }
    
    private void StartProduction()
    {
        isProducing = true;
        productionProgress = 0f;
        
        // 引导完成检测
        if (GuideManager.Instance != null && GuideManager.Instance.IsGuideActive)
            GuideManager.Instance.CompleteCurrentStep();
    }
    
    private void Update()
    {
        if (isProducing)
        {
            productionProgress += Time.deltaTime;
            
            if (productionProgress >= productionTime)
            {
                CompleteProduction();
            }
        }
    }
    
    private void CompleteProduction()
    {
        isProducing = false;
        
        // 生成食物（使用对象池或现有 Prefab）
        GameObject food = GetFoodPrefab();
        if (food != null && spawnPoint != null)
        {
            Instantiate(food, spawnPoint.position, Quaternion.identity);
        }
    }
    
    private GameObject GetFoodPrefab()
    {
        // 根据 foodType 返回对应的 Prefab
        string path = $"Prefabs/Food/{producesFood}";
        return Resources.Load<GameObject>(path);
    }
}

public interface IInteractable
{
    void Interact();
}
```

**验证标准**：
- ✓ 玩家可以启动机器
- ✓ 机器完成生产后生成食物

---

## 步骤 6：实现打包服务

### 6.1 创建 PackingStation.cs

**路径**：`Assets/Scripts/Game/PackingStation.cs`

**代码**：
```csharp
using UnityEngine;

public class PackingStation : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    [SerializeField] private Transform packageSpawnPoint;
    [SerializeField] private int foodPerPackage = 1;
    
    private int currentFoodCount = 0;
    
    public void Interact()
    {
        // 如果有食物，进行打包
        if (currentFoodCount >= foodPerPackage)
        {
            CreatePackage();
        }
    }
    
    public void AddFood()
    {
        currentFoodCount++;
        
        if (currentFoodCount >= foodPerPackage)
        {
            // 引导完成检测
            if (GuideManager.Instance != null && GuideManager.Instance.IsGuideActive)
                GuideManager.Instance.CompleteCurrentStep();
        }
    }
    
    private void CreatePackage()
    {
        currentFoodCount -= foodPerPackage;
        
        // 生成打包好的食物
        GameObject package = GetPackagePrefab();
        if (package != null && packageSpawnPoint != null)
        {
            Instantiate(package, packageSpawnPoint.position, Quaternion.identity);
        }
    }
    
    private GameObject GetPackagePrefab()
    {
        return Resources.Load<GameObject>("Prefabs/Food/Package");
    }
}
```

**验证标准**：
- ✓ 可以添加食物到打包台
- ✓ 打包完成后生成包裹

---

## 步骤 7：创建结算页面

### 7.1 创建 GameOverUI.cs

**路径**：`Assets/Scripts/UI/GameOverUI.cs`

**代码**：
```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text ordersText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button downloadButton;
    
    private void Start()
    {
        gameOverPanel.SetActive(false);
        
        restartButton.onClick.AddListener(OnRestartClicked);
        downloadButton.onClick.AddListener(OnDownloadClicked);
    }
    
    public void ShowGameOver(int score, int orders)
    {
        gameOverPanel.SetActive(true);
        
        // 动画显示分数
        scoreText.text = $"${score}";
        ordersText.text = $"Orders: {orders}";
        
        // 淡入动画
        gameOverPanel.GetComponent<CanvasGroup>().alpha = 0;
        gameOverPanel.GetComponent<CanvasGroup>().DOFade(1f, 0.5f);
        
        // 分数弹出动画
        scoreText.transform.localScale = Vector3.zero;
        scoreText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
        
        // 微信小游戏试玩结束通知
        #if WECHAT_MINI_GAME
        WeChatWASM.WX.NotifyMiniProgramPlayableStatus(
            new WeChatWASM.NotifyMiniProgramPlayableStatusOption() { isEnd = true }
        );
        #endif
    }
    
    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void OnDownloadClicked()
    {
        // 跳转到下载页面或应用商店
        Application.OpenURL("YOUR_DOWNLOAD_URL");
    }
}
```

**验证标准**：
- ✓ 游戏结束时显示结算页
- ✓ 显示正确的分数和订单数
- ✓ 重新开始按钮可用

---

## 步骤 8：最终验证

### 8.1 完整流程测试

**测试步骤**：

```
1. 启动游戏，检查开始页
2. 点击开始，进入游戏
3. 按引导完成第一个订单
4. 自由游戏约20秒
5. 30秒后自动结束
6. 检查结算页显示
7. 点击重玩，检查是否重新开始
```

### 8.2 验证清单

```
□ 开始页标题和按钮显示正常
□ 开始按钮有脉冲动画
□ 点击开始进入游戏场景
□ 引导箭头正确指向目标
□ 订单系统工作正常
□ 食物制作流程完整
□ 打包服务正常
□ 分数正确累计
□ 30秒后游戏结束
□ 结算页显示正确
□ 重玩功能正常
□ Console 无错误
□ 包体大小合理
```

### 8.3 时间验证

```
开始页: 2 秒
游戏玩法: 25 秒
结算页: 3 秒
──────────────
总计: 30 秒 ✓
```

---

## 执行完成标准

完成后应该达到：

1. ✓ 完整的开始页 → 游戏玩法 → 结算页流程
2. ✓ 引导系统帮助玩家快速上手
3. ✓ 核心玩法在30秒内可体验
4. ✓ 结算页有吸引力，引导重玩
5. ✓ Console 无错误
6. ✓ 包体大小合理（< 10MB）

这是一个**完整可玩的餐厅模拟试玩广告**！
