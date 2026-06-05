using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

namespace X4Data
{
    public class XField : XElement
    {
        private string? _val;

        public string Path { get; private init; }
        public new string Value => _val ?? throw new NullReferenceException("Node(struct) field doesn\'t support value");

        public XField(XElement root) : base(root) => Path = root.Name.LocalName;
        public XField(XElement node, XElement member) : base(member)
            => Path = string.Join("::", node.Name.LocalName, member.Name.LocalName);

        public static implicit operator string(XField field) => field.Value;
        public override string ToString()=> _val ?? $"{{[{string.Join(','
                ,string.Join(',',Attributes().Select(attr => attr.Name.LocalName))
                ,string.Join(',',Elements().Select(elem => elem.Name.LocalName)))}]}}";

        public XField this[string field] => Member(field);
        public XField Member(string field)
        {
            _val = Attribute(field)?.Value;
            if (_val != null) return this;
            XElement node = Elements(field).Single();
            return new XField(this, node);
        }

        public XField Seek(string path)
        {
            string[] parts = path.Split("::");
            XField current = this;
            return parts.Aggregate(current, (sub, part) => sub.Member(part));
        }
    }
}
