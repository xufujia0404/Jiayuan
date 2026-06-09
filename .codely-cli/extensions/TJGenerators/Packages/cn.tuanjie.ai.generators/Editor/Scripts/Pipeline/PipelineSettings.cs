#if UNITY_EDITOR
using System;
using TJGenerators;
using TJGenerators.Config;
using TJGenerators.Utils;
using UnityEngine;

namespace TJGenerators.Pipeline
{
    /// <summary>
    /// 从 <see cref="GeneratorConfig"/> 读取生成流水线相关设置（输出类型、后处理与格式转换等）。
    /// </summary>
    public sealed class PipelineSettings
    {
        private static readonly PipelineSettings _default = new PipelineSettings(null);

        /// <summary>当没有具体配置时使用的默认实例（所有属性均返回默认值）。</summary>
        public static PipelineSettings Default => _default;

        private readonly GeneratorConfig _config;

        public PipelineSettings(GeneratorConfig config)
        {
            _config = config;
        }

        public string GetOutputType()
        {
            return !string.IsNullOrEmpty(_config?.outputType) ? _config.outputType : "model";
        }

        /// <summary>
        /// 音频在 API/配置中的格式（如 "wav"、"mp3"）；部分服务标为 "mp4"（实为 MPEG-4 容器内 AAC）。
        /// 落盘与导入时会把 "mp4" 规范为 .m4a，避免 Unity 误判为视频。仅 outputType == "audio" 时有意义。
        /// </summary>
        public string AudioFormat =>
            !string.IsNullOrEmpty(_config?.audioFormat) ? _config.audioFormat : "wav";

        public bool IsThreeDModelOutputType()
        {
            string ot = _config?.outputType;
            if (string.IsNullOrEmpty(ot))
                return true;
            return string.Equals(ot, "model", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ot, "rigged-model", StringComparison.OrdinalIgnoreCase);
        }

        public bool ShouldShowEnableMotionUi() =>
            IsThreeDModelOutputType() && _config?.postProcessing?.enableMotion == true;

        public float GetModelScale()
        {
            return _config?.postProcessing?.modelScale ?? 1f;
        }

        public Vector3 GetModelRotation()
        {
            var rotation = _config?.postProcessing?.rotation;
            if (rotation != null)
                return rotation.ToVector3();
            return new Vector3(0f, 0f, 0f);
        }

        public bool GetPostProcessingIsHumanoid()
        {
            return _config?.postProcessing?.isHumanoid == true;
        }

        public bool GetPostProcessingSingleClipLoopAnimatorController()
        {
            return _config?.postProcessing?.singleClipLoopAnimatorController == true;
        }

        public bool NeedsConversion()
        {
            return !string.IsNullOrEmpty(_config?.postProcessing?.convertFormat);
        }

        public int GetConversionFaceLimit() => 10000;

        public string GetConvertDownloadUrl(TJTaskStatusResponse response)
        {
            if (response?.output?.data?.result == null)
                return null;

            string path = _config?.responseMapping?.convertDownloadUrlPath ?? "model";
            return PathUtils.GetString(response.output.data.result, path);
        }

        public string GetConvertType()
        {
            return _config?.postProcessing?.convertType ?? "taskId";
        }

        public string GetConvertSourceUrl(TJTaskStatusResponse response)
        {
            if (response?.output?.data?.result == null)
                return null;

            string path =
                _config?.postProcessing?.convertSourcePath
                ?? _config?.responseMapping?.downloadUrlPath
                ?? "glb_url";
            return PathUtils.GetString(response.output.data.result, path);
        }

        public string GetConvertEndpoint()
        {
            return _config?.postProcessing?.convertEndpoint ?? _config?.GetEndpoint("convert");
        }
    }
}
#endif
