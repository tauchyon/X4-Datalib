using X4Extractor;

namespace X4Data
{
    public class X4Context
    {
        private static X4Context? _database;

        public static X4Context Context => _database ?? throw new NullReferenceException("Context not initialized");
        public static X4Context Build(string entrypath) => _database is null ? _database = new(X4Wares.Initbase(entrypath)) : throw new InvalidOperationException("Context already initialized");
        private X4Context (Extractor basedata)
        {
            
            WarePool = basedata.Entire.ToDictionary(ware => ware.Id, ware => ware);
            Entire = [.. basedata.Entire];
            Wares = [.. basedata.Basic];
            Products = [.. basedata.Complex];
            Extends = [.. basedata.Endpoint.Cast<EndPoint>()];

            Manufatures = basedata.Bindmethods;
            FactionPool = basedata.Factions.ToDictionary(faction => faction.Id, faction => faction);
            Factions = [.. basedata.Factions];

            Derelations = Factions.SelectMany(fac => fac.Items.Select(item => (Ware: item.Key, Relation: (fac.Id, item.Value))))
                .GroupBy(entry => entry.Ware).ToDictionary(group => group.Key, group => group.Select(entry => entry.Relation).ToList());
        }

        public Dictionary<Wares, IWare> WarePool { get; init; }
        public Dictionary<Wares, List<(Factions, bool)>> Derelations { get; init; }
        public HashSet<IWare> Entire { get; init; }
        public HashSet<Ware> Wares { get; init; }
        public HashSet<Product> Products { get; init; }
        public HashSet<EndPoint> Extends { get; init; }

        public Dictionary<Factions, Methods> Manufatures { get; init; }
        public Dictionary<Factions, Faction> FactionPool { get; init; }
        public HashSet<Faction> Factions { get; init; }
    }
}
