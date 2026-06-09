#if UNITY_EDITOR
using System;
using Codely.Newtonsoft.Json;
using Codely.Newtonsoft.Json.Linq;

namespace TJGenerators.Pipeline
{
    /// <summary>
    /// 模型转换请求数据
    /// </summary>
    [Serializable]
    public class ConvertModelRequestData
    {
        public int faceLimit;
        public string format;
        public string originalModelTaskId;
    }

    /// <summary>
    /// 混元 Motion 任务提交体（与 GeneratorConfig hunyuan-motion 字段对齐）
    /// </summary>
    [Serializable]
    public class HyMotionPostPayload
    {
        public string inputText;
        public float actionDuration = 5f;
        public float cfgStrength = 5f;
        public string randomSeedList = "0";
    }

    /// <summary>
    /// URL格式转换请求数据
    /// </summary>
    public class UrlConversionRequest
    {
        public string glbUrl;
        public string fbxUrl;
        public string objZipUrl;
        public string responseFormat;

        public string ToJson()
        {
            var o = new JObject();
            if (!string.IsNullOrEmpty(glbUrl))
                o["glbUrl"] = glbUrl;
            if (!string.IsNullOrEmpty(fbxUrl))
                o["fbxUrl"] = fbxUrl;
            if (!string.IsNullOrEmpty(objZipUrl))
                o["objZipUrl"] = objZipUrl;
            if (!string.IsNullOrEmpty(responseFormat))
                o["responseFormat"] = responseFormat;
            return o.ToString(Formatting.None);
        }
    }
}
#endif
