using System; using System.Collections.Generic; using System.Globalization; using System.Text; namespace T3Usage {
    internal sealed class JsonObj
    {
        public readonly Dictionary<string, object> Map = new Dictionary<string, object>(StringComparer.Ordinal);
        public readonly List<string> OrderedKeys = new List<string>();

        public void Set(string key, object value)
        {
            if (!Map.ContainsKey(key))
                OrderedKeys.Add(key);
            Map[key] = value;
        }

        public JsonObj GetObj(string key)
        {
            object v;
            return Map.TryGetValue(key, out v) ? (v as JsonObj) : null;
        }

        public List<object> GetArray(string key)
        {
            object v;
            return Map.TryGetValue(key, out v) ? (v as List<object>) : null;
        }

        public string GetString(string key)
        {
            object v;
            if (!Map.TryGetValue(key, out v) || v == null) return null;
            string s = v as string;
            return s != null ? s : v.ToString();
        }

        public bool? GetBool(string key)
        {
            object v;
            if (!Map.TryGetValue(key, out v) || v == null) return null;
            if (v is bool) return (bool)v;
            return null;
        }

        public double? GetDouble(string key)
        {
            object v;
            if (!Map.TryGetValue(key, out v) || v == null) return null;
            if (v is double) return (double)v;
            double d;
            if (double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return null;
        }
    }

    internal static class SimpleJson
    {
        public static JsonObj ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            int idx = 0;
            object v = ParseValue(json, ref idx);
            return v as JsonObj;
        }

        private static void SkipWs(string s, ref int i)
        {
            int n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') i++;
                else break;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObj(s, ref i);
            if (c == '[') return ParseArr(s, ref i);
            if (c == '"') return ParseStr(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') return ParseNull(s, ref i);
            return ParseNum(s, ref i);
        }

        private static JsonObj ParseObj(string s, ref int i)
        {
            JsonObj obj = new JsonObj();
            i++; // '{'
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                if (s[i] != '"') break;
                string key = ParseStr(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object val = ParseValue(s, ref i);
                obj.Set(key, val);
            }
            return obj;
        }

        private static List<object> ParseArr(string s, ref int i)
        {
            List<object> list = new List<object>();
            i++; // '['
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
            }
            return list;
        }

        private static string ParseStr(string s, ref int i)
        {
            i++; // '"'
            int start = i;
            int n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '"')
                {
                    string fast = s.Substring(start, i - start);
                    i++;
                    return fast;
                }
                if (c == '\\')
                    break;
                i++;
            }

            StringBuilder sb = new StringBuilder(64);
            if (i > start)
                sb.Append(s, start, i - start);

            while (i < n)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < n)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= n)
                            {
                                ushort code;
                                if (ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                    sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static object ParseBool(string s, ref int i)
        {
            if (i + 4 <= s.Length && string.CompareOrdinal(s, i, "true", 0, 4) == 0)
            {
                i += 4;
                return true;
            }
            if (i + 5 <= s.Length && string.CompareOrdinal(s, i, "false", 0, 5) == 0)
            {
                i += 5;
                return false;
            }
            i++;
            return false;
        }

        private static object ParseNull(string s, ref int i)
        {
            if (i + 4 <= s.Length && string.CompareOrdinal(s, i, "null", 0, 4) == 0)
                i += 4;
            else
                i++;
            return null;
        }

        private static object ParseNum(string s, ref int i)
        {
            int start = i;
            int n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    i++;
                else
                    break;
            }
            if (i == start) { i++; return 0.0; }
            double d;
            if (double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return 0.0;
        }
    }
}
namespace T3Usage {
internal static class Json {
 public static string Encode(object value){var b=new System.Text.StringBuilder();Write(b,value);return b.ToString();}
 static void Write(System.Text.StringBuilder b,object v){
 if(v==null){b.Append("null");return;}var obj=v as JsonObj;if(obj!=null){Write(b,obj.Map);return;}
 if(v is string||v is char||v is System.DateTime){string s=v is System.DateTime?((System.DateTime)v).ToUniversalTime().ToString("o"):v.ToString();b.Append('"');foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);break;}}b.Append('"');return;}
 if(v is bool){b.Append((bool)v?"true":"false");return;}
 if(v is System.IConvertible){b.Append(System.Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture));return;}
 bool first=true;var map=v as System.Collections.IDictionary;if(map!=null){b.Append('{');foreach(System.Collections.DictionaryEntry e in map){if(!first)b.Append(',');first=false;Write(b,e.Key.ToString());b.Append(':');Write(b,e.Value);}b.Append('}');return;}
 var sequence=v as System.Collections.IEnumerable;if(sequence!=null){b.Append('[');foreach(object e in sequence){if(!first)b.Append(',');first=false;Write(b,e);}b.Append(']');return;}
 b.Append('{');foreach(var f in v.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)){if(!first)b.Append(',');first=false;Write(b,f.Name);b.Append(':');Write(b,f.GetValue(v));}
 foreach(var p in v.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)){if(!p.CanRead||p.GetIndexParameters().Length!=0)continue;if(!first)b.Append(',');first=false;Write(b,p.Name);b.Append(':');Write(b,p.GetValue(v,null));}b.Append('}');
 }
}}
