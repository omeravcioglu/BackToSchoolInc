using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace SchoolSim
{
    /// <summary>Lets a saved object rebuild what it doesn't save (lookups, caches) once it's loaded.</summary>
    public interface ISaveHooks
    {
        void AfterLoad();
    }

    /// <summary>
    /// Writes any plain-data object graph to JSON and reads it back, by reflection: every instance
    /// field (public or private, readonly included) of classes and structs, plus arrays, lists,
    /// dictionaries and sets. Objects referenced twice are written once and linked with "$id" /
    /// "$ref", so shared references (a relationship listed under both students) survive the trip.
    /// Fields marked [NonSerialized] and delegates are skipped. Readonly collections and objects
    /// are filled in place, and fields missing from older files keep their defaults.
    /// </summary>
    public static class SaveSerializer
    {
        // --- Shared type info ---------------------------------------------------------------

        static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        static readonly Dictionary<Type, string[]> NameCache = new Dictionary<Type, string[]>();
        static readonly Dictionary<Type, Dictionary<string, FieldInfo>> FieldMapCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
        static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        /// <summary>Saved fields of a type, base classes first, with the names they're saved under.</summary>
        static FieldInfo[] Fields(Type t, out string[] names)
        {
            lock (FieldCache)
            {
                if (FieldCache.TryGetValue(t, out var cached))
                {
                    names = NameCache[t];
                    return cached;
                }
                var list = new List<FieldInfo>();
                var chain = new List<Type>();
                for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType) chain.Insert(0, c);
                foreach (var c in chain)
                {
                    foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsNotSerialized) continue;
                        if (typeof(Delegate).IsAssignableFrom(f.FieldType)) continue;
                        list.Add(f);
                    }
                }
                var fields = list.ToArray();
                // A base class and a subclass may both have a private field with the same name.
                names = new string[fields.Length];
                var map = new Dictionary<string, FieldInfo>();
                for (int i = 0; i < fields.Length; i++)
                {
                    bool shared = false;
                    for (int j = 0; j < fields.Length; j++)
                        if (j != i && fields[j].Name == fields[i].Name) shared = true;
                    names[i] = shared ? fields[i].DeclaringType.Name + "." + fields[i].Name : fields[i].Name;
                    map[names[i]] = fields[i];
                }
                FieldCache[t] = fields;
                NameCache[t] = names;
                FieldMapCache[t] = map;
                return fields;
            }
        }

        static Dictionary<string, FieldInfo> FieldMap(Type t)
        {
            Fields(t, out _);
            lock (FieldCache) return FieldMapCache[t];
        }

        static bool IsGeneric(Type t, Type definition) => t.IsGenericType && t.GetGenericTypeDefinition() == definition;

        static Type ResolveType(string name)
        {
            lock (TypeCache)
            {
                if (TypeCache.TryGetValue(name, out var t)) return t;
                t = typeof(SaveSerializer).Assembly.GetType(name) ?? Type.GetType(name);
                TypeCache[name] = t;
                return t;
            }
        }

        static object Create(Type t)
        {
            var ctor = t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            return ctor != null ? ctor.Invoke(null) : FormatterServices.GetUninitializedObject(t);
        }

        sealed class ByReference : IEqualityComparer<object>
        {
            public static readonly ByReference Instance = new ByReference();
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }

        // --- Writing ------------------------------------------------------------------------

        /// <summary>Writes one value; <paramref name="ids"/> carries object identities across calls on one file.</summary>
        public sealed class Writer
        {
            readonly StringBuilder _sb;
            readonly Dictionary<object, int> _ids = new Dictionary<object, int>(ByReference.Instance);
            int _next;

            public Writer(StringBuilder sb) => _sb = sb;

            public void Value(object v, Type declared)
            {
                if (v == null)
                {
                    _sb.Append("null");
                    return;
                }
                var t = v.GetType();
                if (t == typeof(string)) { Json.WriteString(_sb, (string)v); return; }
                if (t.IsEnum) { Json.WriteString(_sb, v.ToString()); return; }
                if (t.IsPrimitive || t == typeof(decimal)) { Primitive(v, t); return; }

                if (t.IsArray)
                {
                    var et = t.GetElementType();
                    _sb.Append('[');
                    var arr = (Array)v;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (i > 0) _sb.Append(',');
                        Value(arr.GetValue(i), et);
                    }
                    _sb.Append(']');
                    return;
                }
                if (IsGeneric(t, typeof(List<>)))
                {
                    var et = t.GetGenericArguments()[0];
                    _sb.Append('[');
                    var list = (IList)v;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) _sb.Append(',');
                        Value(list[i], et);
                    }
                    _sb.Append(']');
                    return;
                }
                if (IsGeneric(t, typeof(Dictionary<,>)))
                {
                    var args = t.GetGenericArguments();
                    _sb.Append('[');
                    bool first = true;
                    foreach (DictionaryEntry e in (IDictionary)v)
                    {
                        if (!first) _sb.Append(',');
                        first = false;
                        _sb.Append('[');
                        Value(e.Key, args[0]);
                        _sb.Append(',');
                        Value(e.Value, args[1]);
                        _sb.Append(']');
                    }
                    _sb.Append(']');
                    return;
                }
                if (IsGeneric(t, typeof(HashSet<>)))
                {
                    var et = t.GetGenericArguments()[0];
                    _sb.Append('[');
                    bool first = true;
                    foreach (var item in (IEnumerable)v)
                    {
                        if (!first) _sb.Append(',');
                        first = false;
                        Value(item, et);
                    }
                    _sb.Append(']');
                    return;
                }

                if (t.IsValueType)
                {
                    _sb.Append('{');
                    WriteFields(v, t, false);
                    _sb.Append('}');
                    return;
                }

                if (_ids.TryGetValue(v, out int id))
                {
                    _sb.Append("{\"$ref\":").Append(id.ToString(CultureInfo.InvariantCulture)).Append('}');
                    return;
                }
                id = ++_next;
                _ids[v] = id;
                _sb.Append("{\"$id\":").Append(id.ToString(CultureInfo.InvariantCulture));
                if (t != declared)
                {
                    _sb.Append(",\"$type\":");
                    Json.WriteString(_sb, t.FullName);
                }
                WriteFields(v, t, true);
                _sb.Append('}');
            }

            void WriteFields(object v, Type t, bool comma)
            {
                var fields = Fields(t, out var names);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (comma) _sb.Append(',');
                    comma = true;
                    Json.WriteString(_sb, names[i]);
                    _sb.Append(':');
                    Value(fields[i].GetValue(v), fields[i].FieldType);
                }
            }

            void Primitive(object v, Type t)
            {
                switch (Type.GetTypeCode(t))
                {
                    case TypeCode.Boolean: _sb.Append((bool)v ? "true" : "false"); break;
                    case TypeCode.Char: Json.WriteString(_sb, v.ToString()); break;
                    case TypeCode.Single:
                        float f = (float)v;
                        // NaN and infinity aren't JSON numbers; as strings they still read back.
                        if (float.IsNaN(f) || float.IsInfinity(f)) Json.WriteString(_sb, f.ToString("R", CultureInfo.InvariantCulture));
                        else _sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                        break;
                    case TypeCode.Double:
                        double d = (double)v;
                        string text = d.ToString("R", CultureInfo.InvariantCulture);
                        if (!double.IsNaN(d) && !double.IsInfinity(d) && double.Parse(text, CultureInfo.InvariantCulture) != d)
                            text = d.ToString("G17", CultureInfo.InvariantCulture);   // older runtimes' "R" can lose the last digit
                        if (double.IsNaN(d) || double.IsInfinity(d)) Json.WriteString(_sb, text);
                        else _sb.Append(text);
                        break;
                    default: _sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
                }
            }
        }

        // --- Reading ------------------------------------------------------------------------

        /// <summary>Reads values out of one parsed file, resolving "$ref" links anywhere in it.</summary>
        public sealed class Reader
        {
            readonly Dictionary<int, JsonNode> _byId = new Dictionary<int, JsonNode>();
            readonly Dictionary<int, object> _objects = new Dictionary<int, object>();

            public Reader(JsonNode root) => Index(root);

            void Index(JsonNode n)
            {
                if (n == null) return;
                if (n.Type == JsonNode.Kind.Array)
                {
                    foreach (var item in n.Items) Index(item);
                    return;
                }
                if (n.Type != JsonNode.Kind.Object) return;
                foreach (var m in n.Members)
                {
                    if (m.Key == "$id") _byId[int.Parse(m.Value.Text, CultureInfo.InvariantCulture)] = n;
                    else Index(m.Value);
                }
            }

            /// <summary>Fills an existing object (e.g. a fresh GameState) from a node.</summary>
            public void Populate(JsonNode n, object target)
            {
                var id = n["$id"];
                if (id != null) _objects[int.Parse(id.Text, CultureInfo.InvariantCulture)] = target;
                ReadFields(n, target.GetType(), target);
                if (target is ISaveHooks hooks) _loaded.Add(hooks);
            }

            public object Read(JsonNode n, Type t, object existing)
            {
                if (n == null || n.IsNull)
                    return t.IsValueType && Nullable.GetUnderlyingType(t) == null ? Activator.CreateInstance(t) : null;
                var underlying = Nullable.GetUnderlyingType(t);
                if (underlying != null) t = underlying;

                if (t == typeof(string)) return n.Text;
                if (t.IsEnum) return ReadEnum(n, t);
                if (t == typeof(bool)) return n.Type == JsonNode.Kind.Bool ? n.Bool : n.Text == "true";
                if (t.IsPrimitive || t == typeof(decimal)) return ReadNumber(n, t);

                if (t.IsArray)
                {
                    var et = t.GetElementType();
                    int count = n.Items?.Count ?? 0;
                    var arr = existing is Array old && old.Length == count && old.GetType() == t ? old : Array.CreateInstance(et, count);
                    for (int i = 0; i < count; i++) arr.SetValue(Read(n.Items[i], et, et.IsValueType ? null : arr.GetValue(i)), i);
                    return arr;
                }
                if (IsGeneric(t, typeof(List<>)))
                {
                    var et = t.GetGenericArguments()[0];
                    var list = (IList)(existing ?? Create(t));
                    list.Clear();
                    if (n.Items != null) foreach (var item in n.Items) list.Add(Read(item, et, null));
                    return list;
                }
                if (IsGeneric(t, typeof(Dictionary<,>)))
                {
                    var args = t.GetGenericArguments();
                    var dict = (IDictionary)(existing ?? Create(t));
                    dict.Clear();
                    if (n.Items != null)
                        foreach (var pair in n.Items)
                            dict[Read(pair.Items[0], args[0], null)] = Read(pair.Items[1], args[1], null);
                    return dict;
                }
                if (IsGeneric(t, typeof(HashSet<>)))
                {
                    var et = t.GetGenericArguments()[0];
                    var set = existing ?? Create(t);
                    t.GetMethod("Clear").Invoke(set, null);
                    var add = t.GetMethod("Add");
                    if (n.Items != null) foreach (var item in n.Items) add.Invoke(set, new[] { Read(item, et, null) });
                    return set;
                }

                if (t.IsValueType)
                {
                    var box = Activator.CreateInstance(t);
                    ReadFields(n, t, box);
                    return box;
                }

                var reference = n["$ref"];
                if (reference != null) return Resolve(int.Parse(reference.Text, CultureInfo.InvariantCulture), t);

                var typeName = n["$type"];
                var actual = typeName != null ? ResolveType(typeName.Text) ?? t : t;
                var obj = existing != null && existing.GetType() == actual ? existing : Create(actual);
                var idNode = n["$id"];
                if (idNode != null) _objects[int.Parse(idNode.Text, CultureInfo.InvariantCulture)] = obj;
                ReadFields(n, actual, obj);
                if (obj is ISaveHooks hooks) _loaded.Add(hooks);
                return obj;
            }

            readonly List<ISaveHooks> _loaded = new List<ISaveHooks>();

            /// <summary>Lets every loaded object rebuild its lookups. Call once, after everything is read.</summary>
            public void Finish()
            {
                foreach (var hooks in _loaded) hooks.AfterLoad();
                _loaded.Clear();
            }

            object Resolve(int id, Type declared)
            {
                if (_objects.TryGetValue(id, out var obj)) return obj;
                if (!_byId.TryGetValue(id, out var node)) return null;
                return Read(node, declared, null);
            }

            void ReadFields(JsonNode n, Type t, object obj)
            {
                if (n.Members == null) return;
                var map = FieldMap(t);
                foreach (var m in n.Members)
                {
                    if (m.Key.Length > 0 && m.Key[0] == '$') continue;
                    if (!map.TryGetValue(m.Key, out var f)) continue;   // a field this version doesn't have
                    var current = f.GetValue(obj);
                    var value = Read(m.Value, f.FieldType, current);
                    if (f.FieldType.IsValueType || !ReferenceEquals(value, current)) f.SetValue(obj, value);
                }
            }

            static object ReadEnum(JsonNode n, Type t)
            {
                try
                {
                    return n.Type == JsonNode.Kind.String
                        ? Enum.Parse(t, n.Text)
                        : Enum.ToObject(t, long.Parse(n.Text, CultureInfo.InvariantCulture));
                }
                catch (ArgumentException)
                {
                    return Activator.CreateInstance(t);   // a value this version doesn't know
                }
            }

            static object ReadNumber(JsonNode n, Type t)
            {
                string s = n.Text ?? "0";
                var c = CultureInfo.InvariantCulture;
                switch (Type.GetTypeCode(t))
                {
                    case TypeCode.Single: return float.Parse(s, NumberStyles.Float, c);
                    case TypeCode.Double: return double.Parse(s, NumberStyles.Float, c);
                    case TypeCode.Decimal: return decimal.Parse(s, NumberStyles.Float, c);
                    case TypeCode.Int32: return int.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.UInt32: return uint.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.Int64: return long.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.UInt64: return ulong.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.Int16: return short.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.UInt16: return ushort.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.Byte: return byte.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.SByte: return sbyte.Parse(s, NumberStyles.Integer, c);
                    case TypeCode.Char: return string.IsNullOrEmpty(s) ? '\0' : s[0];
                    default: throw new FormatException("Unsupported number type " + t);
                }
            }
        }

        // --- Convenience ----------------------------------------------------------------------

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            new Writer(sb).Value(value, value?.GetType() ?? typeof(object));
            return sb.ToString();
        }

        public static T Read<T>(string json)
        {
            var root = Json.Parse(json);
            var reader = new Reader(root);
            var value = (T)reader.Read(root, typeof(T), null);
            reader.Finish();
            return value;
        }
    }
}
