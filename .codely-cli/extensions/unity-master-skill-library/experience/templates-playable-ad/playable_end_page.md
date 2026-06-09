# 试玩广告结算页面模板

提供通用的试玩广告结算页面实现，展示分数、成就和重玩引导。

## 适用场景

- 所有试玩广告的结算页面
- 需要展示玩家成绩和引导重玩/下载

## 核心要素

```
结算页面结构：
├── Canvas
│   ├── GameOverPanel
│   │   ├── Score Text（最终分数）
│   │   ├── Stats Text（统计数据）
│   │   ├── Restart Button（重玩按钮）
│   │   └── Download Button（下载按钮，可选）
│   └── Background Overlay（半透明背景）
```

## 输出文件

| 文件 | 路径 | 说明 |
|------|------|------|
| GameOverUI.cs | Assets/Scripts/UI/ | 结算页面管理脚本 |

## 代码模板

### GameOverUI.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text statsText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button downloadButton;
    [SerializeField] private Text downloadButtonText;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float scoreCountDuration = 1f;
    [SerializeField] private float buttonDelay = 0.5f;
    
    [Header("Download Settings")]
    [SerializeField] private string downloadURL = "YOUR_APP_STORE_URL";
    
    [Header("WeChat Mini Game")]
    [SerializeField] private bool notifyWeChatPlayableEnd = true;
    [SerializeField] private float weChatNotifyDelay = 2f;
    
    private int displayScore = 0;
    private int targetScore = 0;
    
    public static GameOverUI Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // 初始化
        gameOverPanel.SetActive(false);
    }
    
    private void Start()
    {
        // 绑定按钮事件
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        
        if (downloadButton != null)
            downloadButton.onClick.AddListener(OnDownloadClicked);
    }
    
    public void ShowGameOver(int score, int coins = 0, string stats = "")
    {
        targetScore = score;
        
        gameOverPanel.SetActive(true);
        canvasGroup.alpha = 0;
        
        // 淡入动画
        canvasGroup.DOFade(1f, fadeInDuration).OnComplete(() => {
            // 开始分数滚动动画
            AnimateScore(score, coins);
            
            // 显示统计数据
            if (statsText != null)
                statsText.text = stats;
            
            // 延迟显示按钮
            DOVirtual.DelayedCall(buttonDelay, ShowButtons);
        });
        
        // 微信小游戏试玩结束通知
        #if WECHAT_MINI_GAME
        if (notifyWeChatPlayableEnd)
        {
            DOVirtual.DelayedCall(weChatNotifyDelay, () => {
                WeChatWASM.WX.NotifyMiniProgramPlayableStatus(
                    new WeChatWASM.NotifyMiniProgramPlayableStatusOption() { isEnd = true }
                );
            });
        }
        #endif
    }
    
    private void AnimateScore(int targetScore, int coins)
    {
        // 分数滚动动画
        displayScore = 0;
        DOTween.To(
            () => displayScore,
            x => {
                displayScore = x;
                if (scoreText != null)
                    scoreText.text = $"${displayScore}";
            },
            targetScore,
            scoreCountDuration
        ).SetEase(Ease.OutQuad);
        
        // 金币显示
        if (coinText != null)
        {
            coinText.text = $"+{coins}";
            coinText.transform.DOScale(1.2f, 0.3f).SetLoops(2, LoopType.Yoyo);
        }
    }
    
    private void ShowButtons()
    {
        // 重玩按钮动画
        if (restartButton != null)
        {
            restartButton.transform.localScale = Vector3.zero;
            restartButton.gameObject.SetActive(true);
            restartButton.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }
        
        // 下载按钮动画
        if (downloadButton != null)
        {
            downloadButton.transform.localScale = Vector3.zero;
            downloadButton.gameObject.SetActive(true);
            downloadButton.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetDelay(0.2f);
        }
    }
    
    private void OnRestartClicked()
    {
        // 播放点击音效
        // AudioManager.Instance?.PlaySFX("button_click");
        
        // 重新加载当前场景
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void OnDownloadClicked()
    {
        // 播放点击音效
        // AudioManager.Instance?.PlaySFX("button_click");
        
        // 打开下载链接
        if (!string.IsNullOrEmpty(downloadURL))
        {
            Application.OpenURL(downloadURL);
        }
    }
    
    // 简化调用
    public void Show(int score)
    {
        ShowGameOver(score);
    }
    
    public void Show(int score, string stats)
    {
        ShowGameOver(score, 0, stats);
    }
}
```

## 调用方式

```csharp
// 在 GameManager 中调用
public void EndGame()
{
    int finalScore = CalculateScore();
    string stats = $"Orders: {ordersCompleted}\nTime: {gameTime:F1}s";
    
    if (GameOverUI.Instance != null)
    {
        GameOverUI.Instance.ShowGameOver(finalScore, coins, stats);
    }
}
```

## 微信小游戏集成

```csharp
// 在游戏结束时通知微信
#if WECHAT_MINI_GAME
using WeChatWASM;

// 在 GameOverUI 中已包含，或手动调用：
WX.NotifyMiniProgramPlayableStatus(new NotifyMiniProgramPlayableStatusOption() 
{ 
    isEnd = true 
});
#endif
```

## UI 层级示例

```
Canvas (Screen Space - Overlay)
├── Background (半透明黑色，alpha = 0.7)
│   └── Image (Stretch to fill)
├── GameOverPanel (居中)
│   ├── Title Text ("GAME OVER" 或 "RESULTS")
│   ├── Score Container
│   │   ├── Label ("SCORE")
│   │   └── Score Text ("$1234")
│   ├── Stats Container
│   │   └── Stats Text
│   ├── Button Container
│   │   ├── Restart Button ("PLAY AGAIN")
│   │   └── Download Button ("DOWNLOAD FULL GAME")
```

## 关键要点

| 要点 | 说明 |
|------|------|
| 分数动画 | 使用 DOTween 实现滚动效果 |
| 按钮延迟 | 先显示分数，再显示按钮 |
| 微信通知 | 调用 WX.NotifyMiniProgramPlayableStatus |

## 已知陷阱

| 陷阱 | 后果 | 解决 |
|------|------|------|
| 场景名错误 | 重玩失败 | 使用 GetActiveScene().name |
| CanvasGroup 缺失 | 淡入效果失效 | 添加 CanvasGroup 组件 |
| 微信 SDK 未集成 | 编译错误 | 使用 #if WECHAT_MINI_GAME |
```