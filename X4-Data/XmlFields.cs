using System.Xml.Linq;

namespace X4Data
{
    public class XField : XElement
    {
        private string? _val;

        public string Path { get; private init; }
        public string Base => base.ToString();
        public new string Value => _val ?? throw new InvalidOperationException("Node(struct) field doesn\'t support value");

        public XField(string path) : this(XDocument.Load(path).Root!) { }
        public XField(XElement root) : base(root) => Path = root.Name.LocalName;
        public XField(XElement node, XElement member) : base(member)
            => Path = string.Join("::", node.Name.LocalName, member.Name.LocalName);

        public static implicit operator string(XField field) => field.Value;
        public override string ToString()=> _val ?? $"{{[{string.Join(','
                ,string.Join(',',Attributes().Select(attr => attr.Name.LocalName))
                ,string.Join(',',Elements().Select(elem => elem.Name.LocalName)))}]}}";

        public XField? Test(string field)
        {
            _val = Attribute(field)?.Value;
            if (_val != null) return this;
            XElement? node = Elements(field).FirstOrDefault();
            return node != null ? new XField(this, node) : null;
        }

        public XField Member(string field)
        {
            _val = Attribute(field)?.Value;
            if (_val != null) return this;
            XElement node = Elements(field).Single();
            return new XField(this, node);
        }

        public XField? Route(string path)
        {
            string[] parts = path.Split("::");
            XField? current = this;
            foreach (string part in parts)
            {
                current = current.Test(part);
                if (current == null) return null;
            }
            return current;
        }

        public XField this[string field] => Seek(field);
        public XField Seek(string path)
        {
            string[] parts = path.Split("::");
            return parts.Aggregate(this, (sub, part) => sub.Member(part));
        }

        public List<XField> Flatten(string match) => Flatten(match, _ => true); 
        public List<XField> Flatten(string match, string value) => Flatten(match, str => str == value);
        public List<XField> Flatten(string match, Func<string, bool> expr)
        {
            var parts = match.Split("::");
            string current = parts[0];
            string pass = string.Join("::", parts.Skip(1));
            if (pass == string.Empty) return Test(current) != null && expr.Invoke(this[current].Value) ? [this[current]] : [];
            List<XField> cache = [];
            if (Element(current) == null) return cache;
            foreach (XElement elem in Elements(current))
            {
                XField field = new XField(this, elem);
                cache.AddRange(field.Flatten(pass, expr));
            }
            return cache;
        }
    }
}