using System.Diagnostics;
using System.Xml.Linq;

namespace X4Extractor
{
    public class Extractor
    {
        private readonly EntryTracker _tracker;
        private readonly Dictionary<TextRef, Methods> _redirects = [];

        private HashSet<XElement> _wareviews;

        public Dictionary<Wares, Gamepart> Partitions = [];
        public Dictionary<Factions, Gamepart> Domains = [];
        public Dictionary<Formula, Gamepart> Recipes = [];

        public List<Faction> Factions { get; } = [];
        public Dictionary<Factions, Methods> Bindmethods { get; } = [];

        public Dictionary<Wares, Tags[]> TagMap { get; } = [];
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

            _wareviews = [.. _tracker.Postreads];
            HashSet<FactionEntry> factions = _tracker.Factions;
            foreach (FactionEntry facentry in factions)
            {
                List<Licenses> licenses = [];

                Races race = Parse<Races>(facentry.Race);
                Factions faction = Parse<Factions>(facentry.Id);
                Domains[faction] = EntryTracker.Tracker.Partitions[facentry.Source];
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
            foreach (XElement xware in _wareviews)
            {
                Debug.Assert(xware.Name == "ware");
                Wares id = xware.Attribute("id")!.Value;
                if (id.Id.Contains("_venture")) continue;

                TagMap[id] = [.. xware.Attribute("tags")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Tags.Tag) ?? []];
                Ware unit = XWare(xware);
                Product? convertible = XProduct(unit, xware);
                IExtendable? extendable = XEndPoint(convertible ?? new Product(unit), xware);
                IWare uware = extendable ?? (IWare)(convertible ?? unit);
                
                Indexing(uware,xware);
                Entire.Add(uware);
                if(extendable != null) Endpoint.Add(extendable);
                else if(convertible != null) Complex.Add(convertible);
                else Basic.Add(unit);
            }
        }

        private void Indexing(IWare uware, XElement xware)
        {
            if (uware is Product product && xware.Element("production") != null)
            {
                Recipes = product.Formulas
                    .Zip(xware.Elements("production"))
                    .ToDictionary(
                        x => x.First,
                        x => Enum.Parse<Gamepart>(x.Second.Attribute("_source")!.Value, true))
                    .Union(Recipes)
                    .ToDictionary();
            }
            if (uware is EndPoint endpoint)
            {
                foreach (Factions faction in endpoint.Economy)
                    Factions.Single(fe => fe.Id == faction).Items[endpoint.Id] = true;
            }

            Partitions[uware.Id] = Enum.Parse<Gamepart>(xware.Attribute("_source")!.Value);
            if(xware.Element("illegal") is null) return;
            foreach (Faction faction in Factions.Where(f => xware.Element("illegal")!.Attribute("factions")!.Value.Contains(f.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
                faction.Items[uware.Id] = false;
        }

        private Ware XWare(XElement xware) => new
        (
            id: xware.Attribute("id")!.Value,
            @class: Enum.Parse<Transports>(xware.Attribute("transport")!.Value, true),
            category: xware.Attribute("group") == null ? null : Enum.Parse<Groups>(xware.Attribute("group")!.Value, true),
            volume: uint.Parse(xware.Attribute("volume")!.Value),
            prices: (
                uint.Parse(xware.Element("price")!.Attribute("min")!.Value),
                uint.Parse(xware.Element("price")!.Attribute("average")!.Value),
                uint.Parse(xware.Element("price")!.Attribute("max")!.Value)
            )
        );

        private Product? XProduct(Ware @base, XElement xware) => xware.Elements("production").Any() ? new Product
        (@base, [.. xware.Elements("production").Select(xproduction =>
            new Formula {
                Time = float.Parse(xproduction.Attribute("time")!.Value),
                Amount = int.Parse(xproduction.Attribute("amount")!.Value),
                Method = _redirects[(TextRef)xproduction.Attribute("name")!.Value],
                Materials = xproduction.Element("primary")?.Elements("ware")
                    .ToDictionary(xw => Wares.Ware(xw.Attribute("ware")!.Value), xw => uint.Parse(xw.Attribute("amount")!.Value))
                    ?? [],
                BounceRate = float.Parse(xproduction.Element("effects")?.Elements("effect")
                    .SingleOrDefault(xe => xe.Attribute("type") is { Value: "work" })?
                    .Attribute("product")?.Value ?? "0")
            })]
        ) : null;

        private EndPoint? XEndPoint(Product @base, XElement xcomponent) => xcomponent.Element("component") != null ? new EndPoint
        (@base,
            source: xcomponent.Element("component")!.Attribute("ref")!.Value,
            restriction:Licenses.Enum(xcomponent.Element("restriction")?.Attribute("licence")!.Value),
            attributes: [.. TagMap[@base.Id]],
            economy: [.. Factions.Where(fe => xcomponent.Elements("owner")
                .Any(xo => xo.Attribute("faction")!.Value.Equals(fe.Id.ToString(), StringComparison.OrdinalIgnoreCase))).Select(f => f.Id)]
        ) : null;
    }
}
