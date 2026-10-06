using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SchoolSim
{
    /// <summary>A parsed JSON value. Numbers keep their text so they convert exactly to any numeric type.</summary>
    public sealed class JsonNode
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type;
        public bool Bool;
        public string Text;                                    // number or string
        public List<JsonNode> Items;                           // array
        public List<KeyValuePair<string, JsonNode>> Members;   // object, in file order

        public JsonNode this[string key]
        {
            get
            {
                if (Members == null) return null;
                foreach (var m in Members)
                    if (m.Key == key) return m.Value;
                return null;
            }
        }

        public bool IsNull => Type == Kind.Null;
    }

    /// <summary>Minimal JSON reading and writing helpers for save files.</summary>
    public static class Json
    {
        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static JsonNode Parse(string text)
        {
            var p = new Parser(text);
            var node = p.Value();
            p.SkipSpace();
            if (!p.AtEnd) throw new FormatException("Unexpected text after the JSON value.");
            return node;
        }

        sealed class Parser
        {
            readonly string _s;
            int _i;

            public Parser(string s) => _s = s;

            public bool AtEnd => _i >= _s.Length;

            public void SkipSpace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            char Peek()
            {
                SkipSpace();
                if (_i >= _s.Length) throw new FormatException("Unexpected end of JSON.");
                return _s[_i];
            }

            void Expect(char c)
            {
                if (Peek() != c) throw new FormatException($"Expected '{c}' at {_i}.");
                _i++;
            }

            public JsonNode Value()
            {
                char c = Peek();
                switch (c)
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return new JsonNode { Type = JsonNode.Kind.String, Text = String() };
                    case 't': Word("true"); return new JsonNode { Type = JsonNode.Kind.Bool, Bool = true };
                    case 'f': Word("false"); return new JsonNode { Type = JsonNode.Kind.Bool, Bool = false };
                    case 'n': Word("null"); return new JsonNode { Type = JsonNode.Kind.Null };
                    default: return Number();
                }
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) throw new FormatException($"Expected {w} at {_i}.");
                _i += w.Length;
            }

            JsonNode Object()
            {
                Expect('{');
                var node = new JsonNode { Type = JsonNode.Kind.Object, Members = new List<KeyValuePair<string, JsonNode>>() };
                if (Peek() == '}')
                {
                    _i++;
                    return node;
                }
                while (true)
                {
                    SkipSpace();
                    string key = String();
                    Expect(':');
                    node.Members.Add(new KeyValuePair<string, JsonNode>(key, Value()));
                    char c = Peek();
                    _i++;
                    if (c == '}') return node;
                    if (c != ',') throw new FormatException($"Expected ',' or '}}' at {_i - 1}.");
                }
            }

            JsonNode Array()
            {
                Expect('[');
                var node = new JsonNode { Type = JsonNode.Kind.Array, Items = new List<JsonNode>() };
                if (Peek() == ']')
                {
                    _i++;
                    return node;
                }
                while (true)
                {
                    node.Items.Add(Value());
                    char c = Peek();
                    _i++;
                    if (c == ']') return node;
                    if (c != ',') throw new FormatException($"Expected ',' or ']' at {_i - 1}.");
                }
            }

            string String()
            {
                if (_i >= _s.Length || _s[_i] != '"') throw new FormatException($"Expected a string at {_i}.");
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _s.Length) throw new FormatException("Unterminated string.");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    char e = _s[_i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }

            JsonNode Number()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (_i == start) throw new FormatException($"Unexpected character '{_s[_i]}' at {_i}.");
                return new JsonNode { Type = JsonNode.Kind.Number, Text = _s.Substring(start, _i - start) };
            }
        }
    }
}
