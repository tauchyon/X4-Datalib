using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace X4Extractor
{
    public struct TextRef
    {
        public static readonly TextRef Invalid = new() { PageId = uint.MaxValue, TextId = uint.MaxValue };

        public uint PageId { get; set; }
        public uint TextId { get; set; }

        public static implicit operator string(TextRef serial) => $"{{{serial.PageId},{serial.TextId}}}";
        public static explicit operator TextRef(string literal)
        {
            if (literal[0] != '{' || literal[^1] != '}') return Invalid;
            string[] strpair = literal.Replace(" ", "")[1..^1].Split(',');
            return new TextRef
            {
                PageId = uint.Parse(strpair[0]),
                TextId = uint.Parse(strpair[1])
            };
        }
    }

    internal class TextPool
    {
        // Maybe add a EnterLazy() method, we don't need this singleton to hold the entire text pages the entire time
        // The symbolizer will handle the most queries, so maybe we can just unload the XElements and read them from disk again when needed

        public static readonly uint[] BurnedPages = [
            20201, 20113, 20107, 20202, 20111, 20104, 20006, 20102,
            20216, 20106, 20101, 20105, 20115, 1001, 20208, 20203,
            1013, 20226, 20403, 20110, 20224, 20114, 20109, 20108
        ]; // result based on activated pages after a full extraction
        public static readonly Dictionary<string, string> LangSelf = new()
        {
            { "\x45\x6e\x67\x6c\x69\x73\x68", "English" },
            { "\x44\x65\x75\x74\x73\x63\x68", "German" },
            { "\x46\x72\x61\x6e\xe7\x61\x69\x73", "French" },
            { "\x49\x74\x61\x6c\x69\x61\x6e\x6f", "Italian" },
            { "\x420\x443\x441\x441\x43a\x438\x439", "Russian" },
            { "\x45\x73\x70\x61\xf1\x6f\x6c", "Spanish" },
            { "\x50\x6f\x72\x74\x75\x67\x75\xea\x73", "Portuguese" },
            { "\x50\x6f\x6c\x73\x6b\x69", "Polish" },
            { "\x10c\x65\x161\x74\x69\x6e\x61", "Czech" },
            { "\x7b80\x4f53\x4e2d\x6587", "Simplified Chinese" },
            { "\x7e41\x9ad4\x4e2d\x6587", "Traditional Chinese" },
            { "\xd55c\xad6d\xc5b4", "Korean" },
            { "\x65e5\x672c\x8a9e", "Japanese" }
        };

        private static TextPool? _textdata;
        private readonly EnvConfig _config;
        private Dictionary<uint, XElement>? _pages;

        public static TextPool Service => _textdata ?? throw new NullReferenceException("TextPool not initialized");
        public HashSet<TextRef> Referenced { get; init; } = [];
        public Dictionary<TextRef, string> Cache { get; init; } = [];

        private TextPool(EnvConfig config)
        {
            config.OnLanguageChanged += (_, _) => Reopen();
            _config = config;
            Reopen();
        }

        public static TextPool Initialize()
        {
            if (_textdata != null)
                throw new InvalidOperationException("TextPool is already initialized.");
            _textdata = new TextPool(EnvConfig.Config);
            return _textdata;
        }

#if DEBUG
        internal uint[] ActivatedPages()
        {
            HashSet<uint> activated = [.. Referenced.Select(textref => textref.PageId)];
            return [.. activated];
        }
#endif

        public void Reopen()
        {
            Cache.Clear();

            string langxml = Path.Combine(_config.BasegamePath,
                string.Format(EnvConfig.LangFileFormat, EnvConfig.I18nCode[_config.Language]));
            _pages = XDocument.Load(langxml).Descendants("page")
                .Select(page => new { Key = uint.Parse(page.Attribute("id")!.Value), Value = page })
                .ToDictionary(route => route.Key, route => route.Value);

            if (Referenced.Count <= 0) return;
            foreach (var textref in Referenced)
                Load(textref);
        }

        public string this[TextRef r] => Load(r);
        public string Load(TextRef serial)
        {
            if (serial == TextRef.Invalid)
                return string.Empty;
            if (Cache.TryGetValue(serial, out string? record))
                return record;
            Referenced.Add(serial);
            XElement page = _pages![serial.PageId];
            XElement? textElement = page.Descendants("t").FirstOrDefault(t => t.Attribute("id")?.Value == serial.TextId.ToString());
            if (textElement == null)
            {
                Console.WriteLine($"[CRITICAL] TextID {serial.TextId} not found in PageID {serial.PageId} ({serial})");
                return $"{{{serial.PageId},{serial.TextId}}}";
            }
            var swap = PostLoad(textElement.Value); // no nesting symbol, linear recursion here.
            Cache[serial] = swap;
            return swap;
        }


        public string PostLoad(string pre) =>
            pre.Contains('$') ? throw new InvalidDataException("Unexpected Injection") : Dereference(Escape(Remove(pre)));

        public static string Escape(string mixed)
        {
            int escserial = mixed.IndexOf("\\0", StringComparison.Ordinal);
            if (escserial == -1) // for "\\033#UI_TAG#some actual text\\033X", remove the \\033s(include 'X') and #UI_TAG#
                return mixed.Replace("\\n", "\n").Replace("\\\'", "'");

            int uiseq = mixed.IndexOf('#');
            mixed = mixed.Remove(uiseq, mixed.LastIndexOf('#') - uiseq + 1);

            int bound = mixed.LastIndexOf("\\0", StringComparison.Ordinal);
            mixed = mixed.Remove(escserial, 4).Remove(bound, 5);

            return mixed.Replace("\\n", "\n").Replace("\\\'", "'");
        }

        public static string Remove(string shallow)
        {
            Stack<int> boundary = new(
                shallow.Select((chr, i) => new { Chr = chr, Index = i })
                    .Where(c => c.Chr == ')').Select(set => set.Index)
                    .Reverse()
                );

            StringBuilder filter = new();
            bool escaped = false;
            for (int offset = 0; offset < shallow.Length; offset++)
            {
                if (shallow[offset] == '(')
                {
                    if (escaped)
                    {
                        filter.Append('(');
                        escaped = false;
                        boundary.Pop();
                        continue;
                    }
                    offset = boundary.Pop();
                    continue;
                }
                if (shallow[offset] == '\\' && (escaped = !escaped))
                    continue;
                escaped = false;
                filter.Append(shallow[offset]);
            }

            return filter.ToString();
        }

        public string Dereference(string literal)
        {
            Debug.Assert(!literal.Contains('\\'));
            if (!literal.Contains('}'))
                return literal;

            Stack<int> boundary = new(
                literal.Select((chr, i) => new { Chr = chr, Index = i })
                    .Where(c => c.Chr == '}').Select(set => set.Index)
                    .Reverse()
                );

            StringBuilder filter = new();
            for (int offset = 0; offset < literal.Length; offset++)
            {
                if (literal[offset] == '{')
                {
                    int exit = boundary.Pop();
                    filter.Append(Load((TextRef)literal[offset..(exit + 1)]));
                    offset = exit;
                    continue;
                }
                filter.Append(literal[offset]);
            }

            return filter.ToString();
        }
    }
}
