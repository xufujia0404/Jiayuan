using UnityEngine;
using UnityEditor;
using Sttop5.Modules.HomeBase;

public class AddPortalEffects
{
    [MenuItem("Portal/Add Effects")]
    public static void Add()
    {
        var tdRune = GameObject.Find("RunePulse");
        if (tdRune != null && tdRune.GetComponent<PortalEffectController>() == null)
        {
            tdRune.AddComponent<PortalEffectController>();
            Debug.Log("Added PortalEffectController to RunePulse");
        }

        var m3Glow = GameObject.Find("PinkGlow");
        if (m3Glow != null && m3Glow.GetComponent<PortalEffectController>() == null)
        {
            m3Glow.AddComponent<PortalEffectController>();
            Debug.Log("Added PortalEffectController to PinkGlow");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Portal effects added!");
    }
}
