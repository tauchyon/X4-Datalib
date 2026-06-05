using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;

namespace X4Extractor
{
    public class Extractor
    {
        private readonly EntryTracker _tracker;
        private readonly HashSet<XElement> _wareviews = [];
        private readonly Dictionary<TextRef, Methods> _redirects = [];

        public Dictionary<object, GamePartition> Datapart;

        public List<Faction> Factions { get; } = [];
        public Dictionary<Factions, Methods> Bindmethods { get; } = [];

        public Dictionary<Wares, Tags[]> Tags { get; } = [];
        public List<IWare> Entire { get; } = [];
        public List<Ware> Basic { get; } = [];
        public List<Product> Complex { get; } = [];
        public List<IExtendable> Endpoint { get; } = [];

        internal Extractor()
            => _tracker = EntryTracker.Tracker;

        public void Extract()
        {
            Collect();
            Resolve();
        }

        internal void Collect()
        {
            static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
                => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result
                    : throw new InvalidDataException($"Invalid {typeof(TEnum).Name} value: '{value}'");

            HashSet<MethodEntry> methods = _tracker.Methods;
            foreach (MethodEntry method in methods)
            {
                Methods valmethod = Parse<Methods>(method.Id);
                _redirects[method.Name] = valmethod;
            }

            _wareviews.UnionWith([.. _tracker.Postreads]);
            HashSet<FactionEntry> factions = _tracker.Factions;
            foreach (FactionEntry facentry in factions)
            {
                List<Licenses> licenses = [];

                Races race = Parse<Races>(facentry.Race);
                Factions faction = Parse<Factions>(facentry.Id);
                foreach (FactionEntry.LicenseEntry lic in facentry.Licenses)
                    licenses.Add(Licenses.License(lic.Id));

                if (methods.Any(me => me.Race == facentry.Race))
                {
                    MethodEntry method = methods.First(me => me.Race == facentry.Race);
                    Methods parsedMethod = Parse<Methods>(method.Id);
                    Bindmethods[faction] = parsedMethod;
                }

                Factions.Add(new Faction
                {
                    Abb = TextPool.Service[facentry.ShortName],
                    Race = race,
                    Id = faction,
                    Licenses = [.. licenses],
                    Items = [],
                });
            }
        }

        internal void Resolve()
        {
            foreach (XElement ware in _wareviews)
            {
                Debug.Assert(ware.Name == "ware");
                Wares id = ware.Attribute("id")!.Value;
                if(id.Id.Contains("_venture")) continue;
                Transports classval = Enum.Parse<Transports>(ware.Attribute("transport")!.Value, true);
                Groups? catval = ware.Attribute("group") == null ? null : Enum.Parse<Groups>(ware.Attribute("group")!.Value, true);
                uint volume = uint.Parse(ware.Attribute("volume")!.Value);
                XElement xprice = ware.Element("price")!; (uint, uint, uint) prices = (
                    uint.Parse(xprice.Attribute("min")!.Value),
                    uint.Parse(xprice.Attribute("average")!.Value),
                    uint.Parse(xprice.Attribute("max")!.Value)
                );

                Reindexing(id, ware);
                Ware unit = new(id, classval, catval, volume, prices);
                Product? convertible = Manufacture(unit, [.. ware.Elements("production")]);
                IExtendable? extendable = Component(convertible ?? new Product(unit), ware);

                Entire.Add(extendable ?? (IWare)(convertible ?? unit));
                if(extendable != null) Endpoint.Add(extendable);
                else if(convertible != null) Complex.Add(convertible);
                else Basic.Add(unit);
            }
        }

        private void Reindexing(Wares id, XElement xware)
        {
            Tags[id] = [.. (xware.Attribute("tags")?.Value ?? "").Split(' ').Select(X4Extractor.Tags.Tag)];
            Factions.Where(fe => xware.Elements("illegal")
                .Any(xi => xi.Attribute("factions")!.Value.Contains(fe.Id.ToString().ToLower()))
                ).ToList().ForEach(fe => fe.Items[id] = false);
        }

        private Product? Manufacture(IWare @base, XElement[] xproductions)
        {
            if(xproductions.Length == 0) return null;
            return new Product(@base, [
                .. xproductions.Select(xp => new Formula {
                        Time = float.Parse(xp.Attribute("time")!.Value),
                        Amount = int.Parse(xp.Attribute("amount")!.Value),
                        Method = _redirects[(TextRef)xp.Attribute("name")!.Value],
                        Materials = xp.Element("primary")?.Elements("ware")
                            .ToDictionary(xw => Wares.Ware(xw.Attribute("ware")!.Value), xw => uint.Parse(xw.Attribute("amount")!.Value))
                            ?? [],
                        BounceRate = float.Parse(xp.Element("effects")?.Elements("effect")
                            .SingleOrDefault(xe => xe.Attribute("type") is { Value: "work" })?
                            .Attribute("product")?.Value ?? "0")
                    }
                )
            ]);
        }

        private IExtendable? Component(Product @base, XElement xinjection)
        {
            if(xinjection.Element("component") == null) return null;
            List<Faction> owners = Factions.Where(fe => xinjection.Elements("owner")
                .Any(xo => string.Compare(xo.Attribute("faction")!.Value, fe.Id.ToString(), true) == 0))
                .ToList(); owners.ForEach(fe => fe.Items[@base.Id] = true);
            return new EndPoint(@base, xinjection.Element("component")!.Attribute("ref")!.Value,
                Licenses.Enum(xinjection.Element("restriction")?.Attribute("licence")!.Value ?? null)) {
                Attributes = [.. Tags[@base.Id]],
                Economy = [.. owners.Select(f => f.Id)]
            };
        }
    }
}
