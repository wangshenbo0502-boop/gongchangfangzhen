using System;
using Unity.VisualScripting.FullSerializer;

namespace WorkshopDemo
{
    internal static class WorkshopResultJson
    {
        internal static fsData Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || fsJsonParser.Parse(json, out var data).Failed || !data.IsDictionary)
                throw new ArgumentException("结果 JSON 格式无效");
            return data;
        }

        internal static fsData Required(fsData parent, string key)
        {
            if (!parent.IsDictionary || !parent.AsDictionary.TryGetValue(key, out var value) || value.IsNull)
                throw new ArgumentException("结果缺少字段 " + key);
            return value;
        }

        internal static void Number(fsData parent, string key)
        {
            var value = Required(parent, key);
            if (!value.IsInt64 && !value.IsDouble) throw new ArgumentException("结果字段不是数值 " + key);
            double number = value.IsDouble ? value.AsDouble : value.AsInt64;
            if (double.IsNaN(number) || double.IsInfinity(number)) throw new ArgumentException("结果含非有限值 " + key);
        }

        internal static void Numbers(fsData parent, string key, bool integers = false)
        {
            var values = Required(parent, key);
            if (!values.IsList) throw new ArgumentException("结果字段不是数组 " + key);
            foreach (var value in values.AsList)
            {
                if (!value.IsInt64 && !value.IsDouble) throw new ArgumentException("结果数组含非数值 " + key);
                double number = value.IsDouble ? value.AsDouble : value.AsInt64;
                if (double.IsNaN(number) || double.IsInfinity(number) ||
                    integers && (number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue))
                    throw new ArgumentException("结果数组数值无效 " + key);
            }
        }
    }
}
