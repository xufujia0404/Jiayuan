# 试玩广告开始页模板

提供通用的试玩广告开始页实现，包含标题、开始按钮和动画效果。

## 适用场景

- 所有试玩广告的开始页面
- 需要吸引玩家点击进入游戏

## 核心要素

```
开始页结构：
├── Canvas
│   ├── Title Text（游戏标题）
│   ├── Start Button（开始按钮，带动画）
│   └── Background（背景图/颜色）
└── Screen Fader（屏幕淡入淡出）
```

## 输出文件

| 文件 | 路径 | 说明 |
|------|------|------|
| MainMenuManager.cs | Assets/Scripts/UI/ | 开始页管理脚本 |

## 代码模板

### MainMenuManager.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DGTweening;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button startButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Image screenFader;
    [SerializeField] private AudioClip tapSound;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float buttonPulseScale = 1.1f;
    [SerializeField] private float buttonPulseDuration = 0.5f;
    
    private void Start()
    {
        // 初始化淡入效果
        if (screenFader != null)
        {
            screenFader.gameObject.SetActive(true);
            screenFader.color = new Color(0, 0, 0, 1);
            screenFader.DOFade(0f, fadeInDuration).OnComplete(() => {
                screenFader.gameObject.SetActive(false);
            });
        }
        
        // 开始按钮脉冲动画（吸引点击）
        if (startButton != null)
        {
            startButton.transform.DOScale(buttonPulseScale, buttonPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
        
        // 标题动画（可选）
        if (titleText != null)
        {
            titleText.transform.DOScale(1.05f, 1f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
    }
    
    private void OnStartButtonClicked()
    {
        // 停止动画
        DOTween.Kill(startButton.transform);
        
        // 播放音效
        if (tapSound != null)
            AudioSource.PlayClipAtPoint(tapSound, Camera.main.transform.position);
        
        // 淡出并进入游戏
        if (screenFader != null)
        {
            screenFader.gameObject.SetActive(true);
            screenFader.DOFade(1f, 0.3f).OnComplete(() => {
                LoadGameScene();
            });
        }
        else
        {
            LoadGameScene();
        }
    }
    
    private void LoadGameScene()
    {
        // 加载游戏场景
        SceneManager.LoadScene("GameScene");
    }
}
```

## 集成说明

1. 创建 Canvas 和 UI 元素
2. 挂载 MainMenuManager 脚本
3. 设置 UI 引用
4. 确保场景名为 "GameScene" 或修改代码中的场景名

## 关键要点

| 要点 | 说明 |
|------|------|
| 按钮动画 | 使用 DOTween 脉冲动画吸引点击 |
| 淡入淡出 | 使用 screenFader 实现平滑过渡 |
| 音效 | 点击时播放音效增强反馈 |

## 已知陷阱

| 陷阱 | 后果 | 解决 |
|------|------|------|
| 场景名错误 | 无法加载游戏场景 | 确认场景名或使用 Build Index |
| 忘记 DOTween.Init | DOTween 不工作 | 在 Awake 中调用 DOTween.Init() |
| Canvas 没有 GraphicRaycaster | UI 点击无响应 | 添加 GraphicRaycaster 组件 |
