using X4Extractor;

namespace X4Data
{
    public enum EWare
    {
        Virtual = 0,
        Basic = 1,
        Complex = 2,
        Extends = 3,
    }

    public class X4Context
    {
        private static X4Context? _database;

        public static X4Context Context => _database ?? throw new NullReferenceException("Context not initialized");
        public static X4Context Build(string entrypath) => _database is null ? _database = new(X4Wares.Initbase(entrypath)) : throw new InvalidOperationException("Context already initialized");
        private X4Context(Extractor basedata)
        {
            Lookup = basedata.Endpoint.Select(e => (e.Id, EWare.Extends))
                .Concat(basedata.Complex.Select(p => (p.Id, EWare.Complex)))
                .Concat(basedata.Basic.Select(w => (w.Id, EWare.Basic)))
                .ToDictionary(x => x.Id, x => x.Item2);

            EndPoints = basedata.Endpoint.Cast<EndPoint>().ToDictionary(endpoint => endpoint.Id, endpoint => endpoint);
            Products = basedata.Complex.ToDictionary(product => product.Id, product => product);
            Wares = basedata.Basic.ToDictionary(ware => ware.Id, ware => ware);
            Any = basedata.Entire.ToDictionary(ware => ware.Id, ware => ware);
            Basic = [.. basedata.Endpoint.Cast<EndPoint>(), .. basedata.Complex, .. basedata.Basic];
            Complex = [.. basedata.Endpoint.Cast<EndPoint>(), .. basedata.Complex];
            Extendable = [.. basedata.Endpoint.Cast<EndPoint>()];
            Entire = [.. basedata.Entire];

            Manufatures = basedata.Bindmethods;
            FactionPool = basedata.Factions.ToDictionary(faction => faction.Id, faction => faction);
            Factions = [.. basedata.Factions];

            Derelations = Factions.SelectMany(fac => fac.Items.Select(item => (Ware: item.Key, Relation: (fac.Id, item.Value))))
                .GroupBy(entry => entry.Ware).ToDictionary(group => group.Key, group => group.Select(entry => entry.Relation).ToList());

            FactionDomains = basedata.Domains;
            WareDomains = basedata.Partitions;
            FormulaDomains = basedata.Recipes;
        }

        public Dictionary<Wares, EWare> Lookup { get; init; }

        public Dictionary<Wares, IWare> Any { get; init; }
        public Dictionary<Wares, Ware> Wares { get; init; }
        public Dictionary<Wares, Product> Products { get; init; }
        public Dictionary<Wares, EndPoint> EndPoints { get; init; }
        public HashSet<IWare> Entire { get; init; }
        public HashSet<Ware> Basic { get; init; }
        public HashSet<Product> Complex { get; init; }
        public HashSet<EndPoint> Extendable { get; init; }

        public Dictionary<Wares, List<(Factions, bool)>> Derelations { get; init; }
        public Dictionary<Factions, Methods> Manufatures { get; init; }
        public Dictionary<Factions, Faction> FactionPool { get; init; }
        public HashSet<Faction> Factions { get; init; }

        public Dictionary<Factions, Gamepart> FactionDomains { get; init; }
        public Dictionary<Wares, Gamepart> WareDomains { get; init; }
        public Dictionary<Formula, Gamepart> FormulaDomains { get; init; }
    }
}
