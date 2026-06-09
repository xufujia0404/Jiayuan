# 试玩广告引导系统模板

提供通用的试玩广告引导系统，帮助玩家快速理解游戏玩法。

## 适用场景

- 所有试玩广告的引导系统
- 需要教会玩家基本操作的游戏

## 核心要素

```
引导系统结构：
├── GuideManager（引导管理器）
│   ├── guideTargets[]（引导目标点列表）
│   └── arrowPrefab（引导箭头）
└── GuideTarget（引导目标点）
    ├── target Transform
    └── completion condition
```

## 输出文件

| 文件 | 路径 | 说明 |
|------|------|------|
| GuideManager.cs | Assets/Scripts/Core/ | 引导管理器 |
| GuideArrow.cs | Assets/Scripts/UI/ | 引导箭头控制器 |
| GuideTarget.cs | Assets/Scripts/Core/ | 引导目标点 |

## 代码模板

### GuideManager.cs

```csharp
using UnityEngine;
using System.Collections.Generic;

public class GuideManager : MonoBehaviour
{
    public static GuideManager Instance { get; private set; }
    
    [Header("Guide Settings")]
    [SerializeField] private List<GuideTarget> guideTargets = new List<GuideTarget>();
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private float arrowHeightOffset = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool autoStart = true;
    
    private int currentIndex = 0;
    private GameObject currentArrow;
    private bool isGuideActive = false;
    
    // 公开属性
    public bool IsGuideActive => isGuideActive;
    public int CurrentStep => currentIndex;
    public int TotalSteps => guideTargets.Count;
    
    private void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (autoStart && guideTargets.Count > 0)
        {
            StartGuide();
        }
    }
    
    public void StartGuide()
    {
        isGuideActive = true;
        currentIndex = 0;
        
        if (guideTargets.Count > 0)
        {
            ActivateTarget(currentIndex);
            ShowArrow(guideTargets[currentIndex].transform);
        }
    }
    
    private void Update()
    {
        if (!isGuideActive || currentArrow == null) return;
        
        // 更新箭头位置
        UpdateArrowPosition();
    }
    
    private void ActivateTarget(int index)
    {
        if (index >= 0 && index < guideTargets.Count)
        {
            guideTargets[index].Activate();
        }
    }
    
    private void ShowArrow(Transform target)
    {
        if (currentArrow != null)
            Destroy(currentArrow);
        
        if (arrowPrefab == null || targetCanvas == null) return;
        
        currentArrow = Instantiate(arrowPrefab, targetCanvas.transform, false);
        currentArrow.name = "GuideArrow";
    }
    
    private void UpdateArrowPosition()
    {
        if (currentIndex >= guideTargets.Count || currentArrow == null) return;
        
        Transform target = guideTargets[currentIndex].transform;
        if (target == null) return;
        
        // 世界坐标转屏幕坐标
        Vector3 worldPos = target.position + Vector3.up * arrowHeightOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        
        currentArrow.transform.position = screenPos;
    }
    
    public void CompleteCurrentStep()
    {
        if (!isGuideActive) return;
        
        // 取消当前目标激活状态
        if (currentIndex < guideTargets.Count)
        {
            guideTargets[currentIndex].Deactivate();
        }
        
        currentIndex++;
        
        if (currentIndex < guideTargets.Count)
        {
            // 还有下一步
            ActivateTarget(currentIndex);
            ShowArrow(guideTargets[currentIndex].transform);
            
            Debug.Log($"[GuideManager] 进入步骤 {currentIndex + 1}/{guideTargets.Count}");
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
        {
            Destroy(currentArrow);
            currentArrow = null;
        }
        
        Debug.Log("[GuideManager] 引导完成，进入自由游戏");
        
        // 可以在这里触发事件或回调
        // OnGuideComplete?.Invoke();
    }
    
    // 编辑器辅助
    private void OnValidate()
    {
        // 自动排序目标点
        guideTargets.Sort((a, b) => a.Order.CompareTo(b.Order));
    }
}
```

### GuideTarget.cs

```csharp
using UnityEngine;
using UnityEngine.Events;

public class GuideTarget : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int order = 0;
    [SerializeField] private string description = "";
    [SerializeField] private float completeDistance = 1f;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onActivated;
    [SerializeField] private UnityEvent onCompleted;
    [SerializeField] private UnityEvent onDeactivated;
    
    private bool isActive = false;
    private bool isCompleted = false;
    
    public int Order => order;
    public string Description => description;
    public bool IsCompleted => isCompleted;
    
    public void Activate()
    {
        isActive = true;
        isCompleted = false;
        onActivated?.Invoke();
        
        Debug.Log($"[GuideTarget] 激活: {description}");
    }
    
    public void Deactivate()
    {
        isActive = false;
        onDeactivated?.Invoke();
    }
    
    public void Complete()
    {
        if (!isActive || isCompleted) return;
        
        isCompleted = true;
        onCompleted?.Invoke();
        
        // 通知 GuideManager
        if (GuideManager.Instance != null)
        {
            GuideManager.Instance.CompleteCurrentStep();
        }
    }
    
    // 用于检测玩家到达（可选）
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || isCompleted) return;
        
        if (other.CompareTag("Player"))
        {
            Complete();
        }
    }
    
    // 编辑器可视化
    private void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.green : Color.gray;
        Gizmos.DrawWireSphere(transform.position, completeDistance);
    }
}
```

### GuideArrow.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GuideArrow : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float bounceHeight = 20f;
    [SerializeField] private float bounceDuration = 0.5f;
    [SerializeField] private float rotateSpeed = 0f;
    
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
        
        // 开始弹跳动画
        StartBounceAnimation();
    }
    
    private void StartBounceAnimation()
    {
        // 上下弹跳
        rectTransform.DOAnchorPosY(originalPosition.y + bounceHeight, bounceDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
        
        // 可选：旋转动画
        if (rotateSpeed > 0)
        {
            rectTransform.DORotate(new Vector3(0, 0, 360), rotateSpeed, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Restart)
                .SetEase(Ease.Linear);
        }
    }
    
    private void OnDestroy()
    {
        DOTween.Kill(rectTransform);
    }
}
```

## 集成说明

1. 将 GuideManager 挂载到场景中的 GameObject 上
2. 创建 GuideTarget 对象并设置引导目标点
3. 创建引导箭头 UI Prefab
4. 在游戏逻辑中调用 `GuideTarget.Complete()` 或通过 Trigger 检测

## 使用示例

```csharp
// 在玩家完成某操作时调用
public void OnPlayerDidSomething()
{
    // 方法1：直接调用 GuideTarget.Complete()
    currentGuideTarget.Complete();
    
    // 方法2：通过 GuideManager（如果需要手动控制）
    // GuideManager.Instance.CompleteCurrentStep();
}
```

## 关键要点

| 要点 | 说明 |
|------|------|
| 单例模式 | GuideManager 使用单例，全局可访问 |
| 顺序引导 | 使用 order 字段排序目标点 |
| 灵活完成条件 | 可通过 Trigger 或代码触发完成 |

## 已知陷阱

| 陷阱 | 后果 | 解决 |
|------|------|------|
| 目标点未排序 | 引导顺序错误 | OnValidate 自动排序 |
| 箭头 Prefab 未设置 | 箭头不显示 | 检查 arrowPrefab 引用 |
| Canvas 引用丢失 | 箭头位置错误 | 设置 targetCanvas |
