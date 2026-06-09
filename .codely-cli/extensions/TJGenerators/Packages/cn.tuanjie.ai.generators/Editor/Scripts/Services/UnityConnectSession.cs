using UnityEditor;
using System.Reflection;

namespace Unity.UniAsset.Manager.Editor.InternalBridge
{
    public class UnityConnectSession
    {
        static UnityConnectSession _instance = new UnityConnectSession();
        
        public static UnityConnectSession instance
        {
            get => _instance;
        }
        
        public string GetAccessToken()
        {
            // 使用反射获取UnityConnect的访问令牌
            var unityConnectType = System.Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
            if (unityConnectType != null)
            {
                var instanceProperty = unityConnectType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var unityConnectInstance = instanceProperty.GetValue(null);
                    var getAccessTokenMethod = unityConnectType.GetMethod("GetAccessToken", BindingFlags.Public | BindingFlags.Instance);
                    if (getAccessTokenMethod != null)
                    {
                        return (string)getAccessTokenMethod.Invoke(unityConnectInstance, null);
                    }
                }
            }
            return "";
        }

        public string GetUserId()
        {
            var unityConnectType = System.Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
            if (unityConnectType != null)
            {
                var instanceProperty = unityConnectType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var unityConnectInstance = instanceProperty.GetValue(null);
                    var getUserIdMethod = unityConnectType.GetMethod("GetUserId", BindingFlags.Public | BindingFlags.Instance);
                    if (getUserIdMethod != null)
                    {
                        return (string)getUserIdMethod.Invoke(unityConnectInstance, null);
                    }
                }
            }
            return "";
        }

        public string GetEnvironment()
        {
            var unityConnectType = System.Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
            if (unityConnectType != null)
            {
                var instanceProperty = unityConnectType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var unityConnectInstance = instanceProperty.GetValue(null);
                    var getEnvironmentMethod = unityConnectType.GetMethod("GetEnvironment", BindingFlags.Public | BindingFlags.Instance);
                    if (getEnvironmentMethod != null)
                    {
                        return (string)getEnvironmentMethod.Invoke(unityConnectInstance, null);
                    }
                }
            }
            return "";
        }

        public void ShowLogin()
        {
            var unityConnectType = System.Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
            if (unityConnectType != null)
            {
                var instanceProperty = unityConnectType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var unityConnectInstance = instanceProperty.GetValue(null);
                    var showLoginMethod = unityConnectType.GetMethod("ShowLogin", BindingFlags.Public | BindingFlags.Instance);
                    showLoginMethod?.Invoke(unityConnectInstance, null);
                }
            }
        }
        
        public static void OpenAuthorizedURLInWebBrowser(string url)
        {
            var unityConnectType = System.Type.GetType("UnityEditor.Connect.UnityConnect,UnityEditor");
            if (unityConnectType != null)
            {
                var instanceProperty = unityConnectType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var unityConnectInstance = instanceProperty.GetValue(null);
                    var openURLMethod = unityConnectType.GetMethod("OpenAuthorizedURLInWebBrowser", BindingFlags.Public | BindingFlags.Instance);
                    openURLMethod?.Invoke(unityConnectInstance, new object[] { url });
                }
            }
        }
    }
} 