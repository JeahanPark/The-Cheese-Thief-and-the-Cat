// MiniJson.cs — lightweight JSON parser for the UI builder (no external dependency).
// Ported/trimmed from phucnguyen752/unity-ui-mcp (MIT). Parser + dictionary helpers only.
//   object o = MiniJson.Deserialize(json);  // object→Dictionary<string,object>, array→List<object>,
//                                            // string→string, number→long|double, bool→bool, null→null
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace McpUiGen.Json
{
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return new Parser(json).ParseValue();
        }

        public static Dictionary<string, object> DeserializeObject(string json)
            => Deserialize(json) as Dictionary<string, object>;

        // ── Dictionary helpers ────────────────────────────────
        public static string GetString(this Dictionary<string, object> dict, string key, string fallback = "")
        {
            if (dict != null && dict.TryGetValue(key, out var v) && v != null) return v.ToString();
            return fallback;
        }

        public static int GetInt(this Dictionary<string, object> dict, string key, int fallback = 0)
        {
            if (dict != null && dict.TryGetValue(key, out var v) && v != null)
            {
                if (v is double d) return (int)d;
                if (v is long l) return (int)l;
                if (int.TryParse(v.ToString(), out var i)) return i;
            }
            return fallback;
        }

        public static float GetFloat(this Dictionary<string, object> dict, string key, float fallback = 0f)
        {
            if (dict != null && dict.TryGetValue(key, out var v) && v != null)
            {
                if (v is double d) return (float)d;
                if (v is long l) return l;
                if (float.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            }
            return fallback;
        }

        public static bool GetBool(this Dictionary<string, object> dict, string key, bool fallback = false)
        {
            if (dict != null && dict.TryGetValue(key, out var v) && v != null)
            {
                if (v is bool b) return b;
                if (bool.TryParse(v.ToString(), out var b2)) return b2;
            }
            return fallback;
        }

        public static Dictionary<string, object> GetObject(this Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out var v)) return v as Dictionary<string, object>;
            return null;
        }

        public static List<object> GetArray(this Dictionary<string, object> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out var v)) return v as List<object>;
            return null;
        }

        // ── Parser ────────────────────────────────────────────
        private class Parser
        {
            private readonly string _json;
            private int _pos;

            public Parser(string json) { _json = json; _pos = 0; }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_pos >= _json.Length) return null;

                char c = _json[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': case 'f': return ParseBool();
                    case 'n': return ParseNull();
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw new FormatException($"Unexpected char '{c}' at position {_pos}");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>();
                _pos++; // skip '{'
                SkipWhitespace();
                if (_pos < _json.Length && _json[_pos] == '}') { _pos++; return dict; }

                while (_pos < _json.Length)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    dict[key] = ParseValue();
                    SkipWhitespace();
                    if (_pos < _json.Length && _json[_pos] == ',') { _pos++; continue; }
                    break;
                }
                SkipWhitespace();
                Expect('}');
                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _pos++; // skip '['
                SkipWhitespace();
                if (_pos < _json.Length && _json[_pos] == ']') { _pos++; return list; }

                while (_pos < _json.Length)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    if (_pos < _json.Length && _json[_pos] == ',') { _pos++; continue; }
                    break;
                }
                SkipWhitespace();
                Expect(']');
                return list;
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (_pos < _json.Length)
                {
                    char c = _json[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (_pos >= _json.Length) break;
                        char esc = _json[_pos++];
                        switch (esc)
                        {
                            case '"':  sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/':  sb.Append('/'); break;
                            case 'b':  sb.Append('\b'); break;
                            case 'f':  sb.Append('\f'); break;
                            case 'n':  sb.Append('\n'); break;
                            case 'r':  sb.Append('\r'); break;
                            case 't':  sb.Append('\t'); break;
                            case 'u':
                                if (_pos + 4 <= _json.Length)
                                {
                                    var hex = _json.Substring(_pos, 4);
                                    sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                                    _pos += 4;
                                }
                                break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                int start = _pos;
                bool isFloat = false;
                if (_pos < _json.Length && _json[_pos] == '-') _pos++;
                while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') _pos++;
                if (_pos < _json.Length && _json[_pos] == '.')
                {
                    isFloat = true; _pos++;
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') _pos++;
                }
                if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    isFloat = true; _pos++;
                    if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') _pos++;
                }
                var numStr = _json.Substring(start, _pos - start);
                if (isFloat) return double.Parse(numStr, CultureInfo.InvariantCulture);
                if (long.TryParse(numStr, out var l)) return l;
                return double.Parse(numStr, CultureInfo.InvariantCulture);
            }

            private object ParseBool()
            {
                if (_pos + 4 <= _json.Length && _json.Substring(_pos, 4) == "true")  { _pos += 4; return true; }
                if (_pos + 5 <= _json.Length && _json.Substring(_pos, 5) == "false") { _pos += 5; return false; }
                throw new FormatException($"Expected bool at position {_pos}");
            }

            private object ParseNull()
            {
                if (_pos + 4 <= _json.Length && _json.Substring(_pos, 4) == "null") { _pos += 4; return null; }
                throw new FormatException($"Expected null at position {_pos}");
            }

            private void SkipWhitespace()
            {
                while (_pos < _json.Length)
                {
                    char c = _json[_pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                    else break;
                }
            }

            private void Expect(char c)
            {
                if (_pos >= _json.Length || _json[_pos] != c)
                    throw new FormatException($"Expected '{c}' at position {_pos}");
                _pos++;
            }
        }
    }
}
