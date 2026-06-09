using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.UI
{
    public class StartGameButton : MonoBehaviour
    {
        [SerializeField] private string sceneName = "GameScene";

        public void OnStartGameClicked()
        {
            // 如果在模块模式下，使用 ModuleManager 进入塔防
            if (Core.ModuleModeDetector.IsModuleMode)
            {
                Sttop5.Shared.Core.ModuleManager.Instance.ActivateModule("towerdefense");
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
