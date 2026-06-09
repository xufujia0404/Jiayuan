#if UNITY_EDITOR
using System;
using Codely.Newtonsoft.Json.Linq;
using TJGenerators.Config;
using Unity.UniAsset.Manager.Editor.InternalBridge;

namespace TJGenerators.AssetSearch
{
    /// <summary>
    /// Codely access_token 换票与缓存入口。UI / CustomTool 只接触 <see cref="GetToken"/> 与 <see cref="Invalidate"/>。
    /// 鉴权失败时抛 <see cref="InvalidOperationException"/>，消息以 <c>AUTH_REQUIRED:</c> 前缀开头，调用方原样呈现给用户。
    /// </summary>
    public static class CodelyTokenProvider
    {
        private const string TokenExchangeEndpoint = "/auth/exchange-with-unity-token";

        private static string _codelyAccessToken;
        private static DateTime _codelyTokenExpiry = DateTime.MinValue;

        /// <summary>
        /// 获取有效的 Codely access_token。缓存未过期直接复用，过期时用 Unity token 重新换票。
        /// </summary>
        /// <exception cref="InvalidOperationException">AUTH_REQUIRED:* 前缀，调用方原样向用户呈现。</exception>
        public static string GetToken()
        {
            if (!string.IsNullOrEmpty(_codelyAccessToken) && DateTime.Now < _codelyTokenExpiry)
                return _codelyAccessToken;
            return ExchangeUnityToken();
        }

        /// <summary>
        /// 强制清空 token 缓存；UI 提供"重新登录"按钮时调用，下次 <see cref="GetToken"/> 会重新换票。
        /// </summary>
        public static void Invalidate()
        {
            _codelyAccessToken = null;
            _codelyTokenExpiry = DateTime.MinValue;
        }

        private static string ExchangeUnityToken()
        {
            string unityToken = UnityConnectSession.instance.GetAccessToken();
            string unityUserId = UnityConnectSession.instance.GetUserId();
            if (string.IsNullOrEmpty(unityToken))
                throw new InvalidOperationException(
                    "AUTH_REQUIRED: Unity token is empty, please sign in to Unity Editor");

            var body = new JObject
            {
                ["unity_access_token"] = unityToken,
                ["unity_user_id"] = unityUserId ?? ""
            };
            string url = ConfigManager.GetCodelyBaseUrl().TrimEnd('/') + TokenExchangeEndpoint;

            string response;
            try
            {
                response = CodelyHttpClient.PostJsonSync(url, body.ToString(), token: "", timeoutSeconds: 10);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"AUTH_REQUIRED: Codely token exchange failed (network error: {ex.Message}). Please sign in to Unity Editor again.");
            }

            JObject data;
            try { data = JObject.Parse(response); }
            catch
            {
                throw new InvalidOperationException(
                    "AUTH_REQUIRED: Codely token exchange returned invalid response. Unity token may be expired, please sign in again.");
            }

            string accessToken = data["access_token"]?.ToString();
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException(
                    "AUTH_REQUIRED: Exchange response missing access_token field");

            int expiresIn = data["expires_in"]?.ToObject<int>() ?? 3600;
            _codelyAccessToken = accessToken;
            _codelyTokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);
            return _codelyAccessToken;
        }
    }
}
#endif
