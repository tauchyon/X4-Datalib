using System.Collections;
using System.Reflection;
using static X4Extractor.Localization;
using static X4Extractor.FactionEntry;

namespace X4Extractor
{
    public class Localization
    {
        internal sealed class IgnoreAttribute : Attribute { }
        internal sealed class IndexSequenceAttribute(int @base) : Attribute
        {
            private static readonly int[] PriUnifiers = [-1, 37, 41, 43, 47, 53, 59, 61];
            public int Base { get; } = @base;
            public int Indexer => PriUnifiers[Base];
        }

        private static readonly Dictionary<Type, int> SequenceCache = [];
        private static readonly Dictionary<Type, string[]> Dereflections = [];
        private static readonly Dictionary<Type, Dictionary<string, int>> ReverseLookup = [];
        private static readonly Dictionary<Int32, TextRef[]> RefPool = [];

        private static Localization? _service;

        private Localization(EntryTracker tracker, TextPool site) => (_tracker, _site) = (tracker, site);
        private readonly EntryTracker _tracker;
        private readonly TextPool _site;

        public readonly Dictionary<Int32, string[]> GlobalPool = [];

        public static Localization Texts =>
            _service ?? throw new InvalidOperationException("Singleton not fully initialized");

        internal static Localization Initialize()
        {
            if (_service != null)
                throw new InvalidOperationException();
            Localization instance = new Localization(EntryTracker.Tracker, TextPool.Service);
            instance.EnsureRecords();
            instance.MountPool();
            EnvConfig.Config.OnLanguageChanged += (_,_) => instance.Reload();
            return _service = instance;
        }

        public string[] this[object ve] => GlobalPool[IndexOf(ve)];

        public void Reload()
        {
            GlobalPool.Clear();
            foreach (var referenceset in RefPool)
                GlobalPool[referenceset.Key] = [.. referenceset.Value.Select(tr => _site[tr])];
        }

        private static void PreMount()
        {
            Type[] hosts = [typeof(Races), typeof(Methods), typeof(Factions), typeof(Groups),
                typeof(Wares), typeof(Licenses)];

            foreach (Type et in hosts)
            {
                var seqAttr = et.GetCustomAttribute<IndexSequenceAttribute>()!;
                SequenceCache[et] = seqAttr.Indexer;
                
                string[] names = et.IsEnum
                    ? Enum.GetNames(et)
                    : Dereflections[et] = [.. (et.GetField("EnumPool")!.GetValue(null) as IDictionary)!.Keys.Cast<string>()] ;

                ReverseLookup[et] = names
                    .Select((name, i) => (name, i))
                    .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
            }
        }

        private void MountPool()
        {
            PreMount();

            HashSet<IEntry> eraser = new();
            eraser.UnionWith(_tracker.Races.Cast<IEntry>());
            eraser.UnionWith(_tracker.Methods.Cast<IEntry>());
            eraser.UnionWith(_tracker.Factions.Cast<IEntry>());
            eraser.UnionWith(_tracker.Licenses.Cast<IEntry>());
            eraser.UnionWith(_tracker.Wares.Cast<IEntry>());
            eraser.UnionWith(_tracker.Groups.Cast<IEntry>());

            Dictionary<Type, Type> symbolref = new()
            {
                [typeof(RaceEntry)] = typeof(Races),
                [typeof(MethodEntry)] = typeof(Methods),
                [typeof(FactionEntry)] = typeof(Factions),
                [typeof(GroupEntry)] = typeof(Groups),
                [typeof(WareEntry)] = typeof(Wares),
                [typeof(LicenseEntry)] = typeof(Licenses)
            };

            foreach (IEntry entry in eraser)
            {
                Type @class = symbolref[entry.GetType()];
                int indexbase = SequenceCache[@class];
                Dictionary<string, int> lookup = ReverseLookup[@class];
                int index = indexbase * lookup[entry.Id];
                RefPool[index] = [.. entry.TextRefs.Select(tr => tr.Ref)];
                GlobalPool[index] = [.. entry.TextRefs.Select(tr => _site[tr.Ref])];
            }
        }

        public static int IndexOf(object venum)
        {
            Type @type = venum.GetType();
            int prime = SequenceCache[@type];
            Dictionary<string, int> loopup = ReverseLookup[@type];
            return prime * loopup[Convert.ToString(venum)!];
        }

        private void EnsureRecords()
        {
            var provider = new (string Label, Func<HashSet<string>> Accessor, Func<HashSet<string>, HashSet<string>?> Match)[]
            {
                ("Races", _tracker.AccRaces, Match<Races>),
                ("Methods", _tracker.AccMethods, Match<Methods>),
                ("Factions", _tracker.AccFactions, Match<Factions>),
                ("Transports", _tracker.AccTransports, Match<Transports>),
                ("Groups", _tracker.AccGroups, Match<Groups>),
            };

            foreach (var (label, accessor, match) in provider)
            {
                HashSet<string> symbols = accessor();
                HashSet<string>? diff = match(symbols);
                if (diff != null)
                    Console.WriteLine($"[CRITICAL] {label} Enum Data mismatched: [{string.Join(',', diff)}]");
            }

            _ = _tracker.AccLicences().Select(Licenses.License).ToArray();
            _ = _tracker.AccWares().Select(Wares.Ware).ToArray();
            _ = _tracker.AccTags().Select(Tags.Tag).ToArray();
        }

        internal static HashSet<string>? Match<TEnum>(HashSet<string> symbol) where TEnum : struct, Enum
        {
            Type etype = typeof(TEnum);
            HashSet<FieldInfo> finfo = [.. etype.GetFields()];
            foreach (FieldInfo efield in finfo)
            {
                if (efield.IsSpecialName)
                    finfo.Remove(efield);
                else if (Attribute.IsDefined(efield, typeof(IgnoreAttribute)) || symbol.Contains(efield.Name.ToLower()))
                {
                    symbol.Remove(efield.Name.ToLower());
                    finfo.Remove(efield);
                }
            }
            return finfo.Count == 0 && symbol.Count == 0 ? null : symbol;
        }
    }

    [IndexSequence(0)]
    public readonly record struct Wares
    {
        public static readonly Dictionary<string, Wares> EnumPool = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<Wares, int> EnumIndex = [];

        private static int _enumCount = 0;

        public string Id { get; }
        public Int32 Uid => GetHashCode() & ~(1 << 31);

        private Wares(string id)
        {
            EnumPool[Id = id] = this;
            EnumIndex[this] = _enumCount++;
        }

        public static int Index(string licence) => EnumIndex[licence];
        public static explicit operator int(Wares enumval) => EnumIndex[enumval];
        public static implicit operator string(Wares license) => license.ToString();
        public static implicit operator Wares(string id) => EnumPool.TryGetValue(id, out Wares venum)
            ? venum : throw new InvalidOperationException("VEnum entry not found");
        public override string ToString() => char.ToUpper(Id[0]) + Id.Substring(1);
        public override int GetHashCode() => Id.GetHashCode();

        public bool Equals(Wares? other) => other != null && string.Equals(this.Id, other.Value.Id, StringComparison.OrdinalIgnoreCase);
        public static bool operator ==(Wares? left, Wares? right) => left.Equals(right);
        public static bool operator !=(Wares? left, Wares? right) => !left.Equals(right);

        public static Wares Ware(string id) =>
            EnumPool.TryGetValue(id.ToLower(), out Wares venum) ? venum : new Wares(id);
        public static Wares? Enum(string? id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return EnumPool[id];
        }
    }

    [IndexSequence(1)]
    public enum Races : byte
    {
        Argon, Boron, Terran,
        Paranid, Teladi, Split,
        Khaak, Xenon,
        [Ignore] Drone,
    }

    [IndexSequence(2)]
    public enum Methods : byte
    {
        Argon, Paranid, Teladi,
        Terran, Boron, Split, Xenon,
        Processing, Recycling, Research,
        Closedloop, Default,
        [Ignore] Universal
    }

    [IndexSequence(3)]
    public enum Factions : byte
    {
        Argon, Paranid, Teladi, Terran, Split,
        Antigone, Holyorder, Ministry, Pioneers, Freesplit,
        Hatikvah, Alliance, Scaleplate, Yaki, Fallensplit,
        Boron, Trinity, Kaori, Loanshark, Scavenger,
        Court, Buccaneers, Xenon, Khaak,
        [Ignore] Player, [Ignore] Hidden,
    }

    [IndexSequence(4)]
    public record struct Licenses
    {
        public static readonly Dictionary<string, Licenses> EnumPool = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<Licenses, int> EnumIndex = [];

        public static readonly Licenses GeneralShip = new("generaluseship");
        public static readonly Licenses GeneralEquipment = new("generaluseequipment");
        public static readonly Licenses MilitaryShip = new("militaryship");
        public static readonly Licenses MilitaryEquipmentt = new("militaryequipment");
        public static readonly Licenses CapitalShip = new("capitalship");
        public static readonly Licenses CapitalEquipment = new("capitalequipment");
        public static readonly Licenses WharfBuilding = new("station_equip_sm");
        public static readonly Licenses ShipyardBuilding = new("station_equip_lxl");
        public static readonly Licenses GenBasicModule = new("station_gen_basic");
        public static readonly Licenses GenMediumModule = new("station_gen_intermediate");
        public static readonly Licenses GenAdvanceModule = new("station_gen_advanced");

        private static int _enumCount = 0;

        public string Id { get; }

        private Licenses(string id)
        {
            EnumPool[Id = id] = this;
            EnumIndex[this] = _enumCount++;
        }

        public static int Index(string licence) => EnumIndex[licence];
        public static explicit operator int(Licenses enumval) => EnumIndex[enumval];
        public static implicit operator string(Licenses license) => license.ToString();
        public static implicit operator Licenses(string id) => EnumPool.TryGetValue(id, out Licenses venum)
            ? venum : throw new InvalidOperationException("VEnum entry not found");
        public override string ToString() => char.ToUpper(Id[0]) + Id.Substring(1);
        public override int GetHashCode() => Id.GetHashCode();

        public bool Equals(Licenses? other) => other != null && string.Equals(this.Id, other.Value.Id, StringComparison.OrdinalIgnoreCase);
        public static bool operator ==(Licenses? left, Licenses? right) => left.Equals(right);
        public static bool operator !=(Licenses? left, Licenses? right) => !left.Equals(right);

        public static Licenses License(string id) =>
            EnumPool.TryGetValue(id.ToLower(), out Licenses venum) ? venum : new Licenses(id);
        public static Licenses? Enum(string? id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return EnumPool[id];
        }
    }

    [IndexSequence(5)]
    public enum Groups : byte
    {
        Hightech, Energy, Shiptech, Agricultural, Pharmaceutical,
        Engines, Thrusters, Shields, Weapons, Turrets, Missiles, Drones,
        Refined, Food, Minerals, Gases, Ice, Water,
        Software, Hardware, Countermeasures,
        Generalitem, Contraband, Luxuryitem, Curiosity,
    }

    // [IndexSequence(6)] The Transports text are coded in scripts
    public enum Transports : byte
    {
        Solid, Liquid, Container, Condensate,
        Ship, Equipment, Software,
        Workunit, Passenger, Research,
        Inventory,
    }

    public readonly record struct Tags
    {
        public static readonly Dictionary<string, Tags> EnumPool = new(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<Tags, int> EnumIndex = [];

        public static readonly Tags NoplayerBlueprint = new("noplayerblueprint");
        public static readonly Tags NoplayerBuild = new("noplayerbuild");
        public static readonly Tags Deprecated = new("deprecated");
        public static readonly Tags Hidden = new("hidden");
        public static readonly Tags Container = new("container");
        public static readonly Tags Solid = new("solid");
        public static readonly Tags Liquid = new("liquid");
        public static readonly Tags Condensate = new("condensate");
        public static readonly Tags Inventory = new("inventory");
        public static readonly Tags Economy = new("economy");
        public static readonly Tags Minable = new("minable");
        public static readonly Tags Crafting = new("crafting");
        public static readonly Tags Workunit = new("workunit");
        public static readonly Tags Ship = new("ship");
        public static readonly Tags Module = new("module");
        public static readonly Tags Equipment = new("equipment");

        private static int _enumCount = 0;

        public string Id { get; }

        private Tags(string id)
        {
            EnumPool[Id = id] = this;
            EnumIndex[this] = _enumCount++;
        }

        public static int Index(string tag) => EnumIndex[tag];
        public static explicit operator int(Tags enumval) => EnumIndex[enumval];
        public static implicit operator string(Tags tag) => tag.ToString();
        public static implicit operator Tags(string id) => EnumPool.TryGetValue(id.ToLower(), out Tags venum)
            ? venum : throw new InvalidOperationException("VEnum entry not found");
        public override string ToString() => char.ToUpper(Id[0]) + Id.Substring(1);
        public override int GetHashCode() => Id.GetHashCode();

        public bool Equals(Tags? other) => other != null && string.Equals(this.Id, other.Value.Id, StringComparison.OrdinalIgnoreCase);
        public bool Equals(string? entry) => entry != null && string.Equals(this.Id, entry, StringComparison.OrdinalIgnoreCase);
        public static bool operator==(Tags? left, Tags? right) => left.Equals(right);
        public static bool operator!=(Tags? left, Tags? right) => !left.Equals(right);

        public static Tags Tag(string id) =>
            EnumPool.TryGetValue(id.ToLower(), out Tags venum) ? venum : new Tags(id);
        public static Tags? Enum(string? id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return EnumPool[id];
        }
    }

}
