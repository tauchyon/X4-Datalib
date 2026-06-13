using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("X4-Data")]
// possible partial functionalities

namespace X4Extractor
{
    public class Faction
    {
        public string Abb { get; init; }
        public Races Race { get; init; }
        public Factions Id { get; init; }
        public Licenses[] Licenses { get; init; }
        public Dictionary<Wares, bool> Items { get; init; }
    }

    public interface IWare
    {
        public Wares Id { get; }
        public uint Volume { get; }
        public (uint Min, uint Avg, uint Max) Prices { get; }
        public Transports Class { get; }
        public Groups? Category { get; }
    }

    public partial record Ware : IWare
    {
        public Wares Id { get; init; }
        public Transports Class { get; init; }
        public Groups? Category { get; init; }
        public uint Volume { get; init; }
        public (uint Min, uint Avg, uint Max) Prices { get; init; }
        public Ware(IWare @base) : this(@base.Id, @base.Class, @base.Category, @base.Volume, @base.Prices) { }
        public Ware(Wares id, Transports @class, Groups? category, uint volume, (uint Min, uint Avg, uint Max) prices)
            => (Id, Class, Category, Volume, Prices) = (id, @class, category, volume, prices);
    }

    public partial record Formula
    {
        public Dictionary<Wares, uint> Materials { get; init; } = [];
        public float Time { get; init; }
        public int Amount { get; init; }
        public Methods Method { get; init; }
        public float BounceRate { get; init; }
    }

    public partial record Product : Ware
    {
        public List<Formula> Formulas { get; }
        public Product(IWare @base, params Formula[] manufacture) : base(@base)
            => Formulas = [.. manufacture];
    }

    public interface IExtendable : IWare
    {
        public object Source { get; }
        public List<Factions> Economy { get; }
        public Licenses? Restriction { get; }
    }

    public partial record EndPoint : Product, IExtendable
    {
        object IExtendable.Source => Source;
        public string Source { get; init; }
        public List<Tags> Attributes { get; init; } = [];
        public List<Factions> Economy { get; init; } = [];
        public Licenses? Restriction { get; init; }

        public EndPoint(Product @base, string source, Licenses? restriction,
            List<Tags> attributes, List<Factions> economy) : base(@base)
            => (Source, Restriction, Attributes, Economy) = (source, restriction, attributes, economy);
    }
}