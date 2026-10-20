using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace CubeLauncher.Core
{
    /// <summary>
    /// Small, dependency-free JSON helper built on the .NET Framework JavaScriptSerializer.
    /// Keeps the rest of the launcher free of third party assemblies.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 256;
            return serializer.DeserializeObject(text);
        }

        public static string Stringify(object value)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 256;
            return serializer.Serialize(value);
        }
    }

    public sealed class JMap
    {
        private readonly Dictionary<string, object> _values;

        public JMap()
        {
            _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public JMap(object value)
        {
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map == null)
            {
                _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _values = new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);
            }
        }

        public static JMap Parse(string text)
        {
            return new JMap(Json.Parse(text));
        }

        public IDictionary<string, object> Raw
        {
            get { return _values; }
        }

        public int Count
        {
            get { return _values.Count; }
        }

        public bool Has(string key)
        {
            return key != null && _values.ContainsKey(key);
        }

        public object Get(string key)
        {
            object value;
            return key != null && _values.TryGetValue(key, out value) ? value : null;
        }

        public void Set(string key, object value)
        {
            _values[key] = value;
        }

        public JMap Map(string key)
        {
            return new JMap(Get(key));
        }

        public IList<object> List(string key)
        {
            return AsList(Get(key));
        }

        public List<JMap> Maps(string key)
        {
            List<JMap> result = new List<JMap>();
            IList<object> list = List(key);
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is IDictionary) result.Add(new JMap(list[i]));
            }
            return result;
        }

        public string Str(string key, string fallback)
        {
            object value = Get(key);
            if (value == null) return fallback;
            return Convert.ToString(value);
        }

        public string Str(string key)
        {
            return Str(key, "");
        }

        public int Int(string key, int fallback)
        {
            object value = Get(key);
            if (value == null) return fallback;
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        public int Int(string key)
        {
            return Int(key, 0);
        }

        public long Long(string key, long fallback)
        {
            object value = Get(key);
            if (value == null) return fallback;
            try { return Convert.ToInt64(value); }
            catch { return fallback; }
        }

        public long Long(string key)
        {
            return Long(key, 0);
        }

        public bool Bool(string key, bool fallback)
        {
            object value = Get(key);
            if (value == null) return fallback;
            if (value is bool) return (bool)value;
            bool parsed;
            if (bool.TryParse(Convert.ToString(value), out parsed)) return parsed;
            return fallback;
        }

        public bool Bool(string key)
        {
            return Bool(key, false);
        }

        public static IList<object> AsList(object value)
        {
            IList<object> generic = value as IList<object>;
            if (generic != null) return generic;

            IList list = value as IList;
            if (list == null) return null;
            List<object> copy = new List<object>(list.Count);
            for (int i = 0; i < list.Count; i++) copy.Add(list[i]);
            return copy;
        }
    }

    public sealed class JList
    {
        private readonly IList<object> _items;

        public JList(object value)
        {
            _items = JMap.AsList(value) ?? new List<object>();
        }

        public IList<object> Raw
        {
            get { return _items; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public object Get(int index)
        {
            if (index < 0 || index >= _items.Count) return null;
            return _items[index];
        }

        public JMap Map(int index)
        {
            return new JMap(Get(index));
        }

        public string Str(int index)
        {
            object value = Get(index);
            return value == null ? "" : Convert.ToString(value);
        }

        public List<JMap> Maps()
        {
            List<JMap> result = new List<JMap>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] is IDictionary) result.Add(new JMap(_items[i]));
            }
            return result;
        }
    }

    public static class JsonExtensions
    {
        public static string StringOrEmpty(this JMap map, string key)
        {
            return map == null ? "" : map.Str(key);
        }
    }
}
