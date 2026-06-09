using UnityEngine;
using TowerDefense.Tower;

public class BuildButtonManager : MonoBehaviour
{
    public static BuildButtonManager Instance { get; private set; }
    
    [Tooltip("建造按钮预制体")]
    public GameObject buildButtonPrefab;
    
    [Tooltip("Canvas引用")]
    public Canvas uiCanvas;
    
    [Tooltip("按钮偏移")]
    public Vector2 buttonOffset = new Vector2(0, 50);
    
    private GameObject _currentButton;
    private TowerSlot _currentSlot;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }
    }
    
    /// <summary>
    /// 显示建造按钮
    /// </summary>
    public void ShowBuildButton(TowerSlot slot)
    {
        // 如果有其他按钮，先隐藏
        HideBuildButton();
        
        if (buildButtonPrefab == null || uiCanvas == null)
        {
            Debug.LogError("BuildButtonManager: Missing prefab or Canvas!");
            return;
        }
        
        // 计算按钮位置
        Vector3 worldPos = Camera.main.WorldToScreenPoint(slot.transform.position);
        worldPos += new Vector3(buttonOffset.x, buttonOffset.y, 0);
        
        // 实例化按钮
        _currentButton = Instantiate(buildButtonPrefab);
        _currentButton.transform.SetParent(uiCanvas.transform, false);
        _currentButton.transform.position = worldPos;
        
        // 绑定事件
        UnityEngine.UI.Button button = _currentButton.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnBuildButtonClicked(slot));
        }
        
        _currentSlot = slot;
        Debug.Log($"Build button shown for slot: {slot.name}");
    }
    
    /// <summary>
    /// 隐藏建造按钮
    /// </summary>
    public void HideBuildButton()
    {
        if (_currentButton != null)
        {
            Destroy(_currentButton);
            _currentButton = null;
        }
        _currentSlot = null;
    }
    
    /// <summary>
    /// 建造按钮点击
    /// </summary>
    private void OnBuildButtonClicked(TowerSlot slot)
    {
        Debug.Log("Build button clicked!");
        
        // 先隐藏按钮
        HideBuildButton();
        
        // 让槽位自己处理建造逻辑
        // 这里我们直接调用一个方法
        slot.OnBuildButtonClickedByManager();
    }
    
    /// <summary>
    /// 检查当前是否显示了按钮
    /// </summary>
    public bool IsButtonShowing()
    {
        return _currentButton != null;
    }
}