using System.Numerics;
using X4Extractor;

namespace X4Data
{
    internal static class Distiller
    {
        internal static readonly string[] IgnoreTags = ["symmetry", "hit", "collision", "part", "component"];

        public static IEnumerable<Slot> Slots(XField connections)
        {
            IEnumerable<XField> targets = connections.Flatten("connection::tags", tagstr => /*tagstr.Contains("component") ||*/
                Enum.GetValues<Connections>().Any(c => tagstr.Split(' ').Any(s => s.Equals(c.ToString(), StringComparison.OrdinalIgnoreCase))));

            foreach (XField con in targets)
            {
                List<Tags> tags = [.. con["tags"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !IgnoreTags.Any(t.Contains)).Select(Tags.Tag)];
                Connections self = tags.Select(t => Enum.TryParse(t, true, out Connections type) ? (Connections?)type : null).Single(c=>c.HasValue)!.Value;
                Extents tier = tags.Select(t => Enum.TryParse(t, true, out Extents type) ? (Extents?)type : null).SingleOrDefault(c => c.HasValue) ?? Extents.Invalid;
                ComType[] types = [.. tags.Select(t => Enum.TryParse(t, true, out ComType type) ? (ComType?)type : null).Where(t => t.HasValue).Select(t=> t!.Value)];
                tags.RemoveAll(tag => tag.Equals(self.ToString()) || tag.Equals(tier.ToString()) || types.Any(type => tag.Equals(type.ToString())));
                types = types.Length == 0 ? [ComType.Dedicated] : types;

                XField? transform = con.Test("offset");
                if (transform?.Test("position") == null)
                {
                    yield return new Slot { Category = self, Tier = tier, Types = types, Group = con.Test("group")?.Value, Detail = [..tags] };
                    continue;
                }

                (Vector3 Pos, Quaternion Rot) offset = default;
                XField position = transform!["position"];
                offset.Pos = new Vector3(float.Parse(position["x"]), float.Parse(position["y"]), float.Parse(position["z"]));
                XField? quaternion = transform.Test("quaternion");
                if(quaternion != null)
                    offset.Rot = new Quaternion(float.Parse(quaternion["qx"]), float.Parse(quaternion["qy"]), float.Parse(quaternion["qz"]), float.Parse(quaternion["qw"]));
                yield return new Slot { Category = self, Tier = tier, Types = types, Group = con.Test("group")?.Value, Detail = [..tags], Offset = offset};
            }
        }

        public static X4Context Reindex(X4Context site)
        {
            HashSet<Extendable> units = [];
            foreach (EndPoint endpoint in site.Extendable)
                units.Add(new Extendable(endpoint));
            site.Relays = units.Select(unit => (unit.Id, unit)).ToDictionary(tup => tup.Id, tup => tup.unit);
            site.Components = units;
            return site;
        }
    }
}
