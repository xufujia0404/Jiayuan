#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Codely.Newtonsoft.Json.Linq;
using TJGenerators.Config;

namespace TJGenerators.Generators
{
    /// <summary>
    /// 将 <see cref="ParameterConfig"/> 与用户取值写入请求 <see cref="JObject"/>。
    /// </summary>
    internal static class ParameterJsonWriter
    {
        public static void Apply(
            JObject root,
            IReadOnlyList<ParameterConfig> parameters,
            IReadOnlyDictionary<string, object> values,
            string inputMode
        )
        {
            if (parameters == null || root == null || values == null)
                return;

            foreach (var param in parameters)
            {
                if (!values.TryGetValue(param.id, out object value))
                    continue;

                string fieldName = param.GetApiFieldName(inputMode);

                switch (param.type)
                {
                    case "int":
                        root[fieldName] = Convert.ToInt32(value);
                        break;
                    case "float":
                        root[fieldName] = Convert.ToSingle(value);
                        break;
                    case "bool":
                        root[fieldName] = Convert.ToBoolean(value);
                        break;
                    case "dropdown":
                        string strVal = value?.ToString() ?? "";
                        if (param.valueType == "string")
                            root[fieldName] = strVal;
                        else if (int.TryParse(strVal, out int intVal))
                            root[fieldName] = intVal;
                        else if (float.TryParse(strVal, out float floatVal))
                            root[fieldName] = floatVal;
                        else
                            root[fieldName] = strVal;
                        break;
                    case "json":
                        string jsonVal = value?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(jsonVal))
                            root[fieldName] = JToken.Parse(jsonVal);
                        break;
                    default:
                        root[fieldName] = value?.ToString() ?? "";
                        break;
                }
            }
        }
    }
}

#endif
