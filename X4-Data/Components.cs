using System.Diagnostics;
using X4Extractor;

namespace X4Data
{
    public enum Macros : byte
    {
        ShipS, ShipM, ShipL, ShipXL, ShipXS,
        Engine, Shieldgenerator, Weapon, Turret,
        Missilelauncher, Missileturret, Missile,
        Bomblauncher, Bomb, Countermeasure, Mine,
        Navbeacon, Satellite, Resourceprobe,

        Dockarea, Pier, Buildmodule, Production,
        Defencemodule, Processingmodule, Connectionmodule,
        Welfaremodule, Habitation,

        Storage, Radar, Scanner, Destructible,
        Collectablewares, Lockbox,
    };

    public interface IReference
    {
        public string Name { get; }
        public Macros Class { get; }
        public XField Property { get; }
    }

    public class Profile : IReference
    {
        private WeakReference<XField>? _cached;

        public string Name { get; }
        public string Path { get; }
        public string Target { get; }
        public Macros Class { get; }
        public XField Property
        {
            get
            {
                if (_cached?.TryGetTarget(out XField? loaded) == true) return loaded;
                loaded = new XField(Path)["macro::properties"];
                _cached = new WeakReference<XField>(loaded);
                return loaded;
            }
        }

        public Profile(EndPoint source, out XField macro) : this(source.Source, out macro) { }
        private Profile(string path, out XField macro)
        {
            Name = path;
            Path = EntryTracker.Tracker.Redirections[Name];
            macro = new XField(Path); Target = macro["macro::component::ref"];
            Class = Enum.Parse<Macros>(new XField(Path)["macro::class"].Value.Replace("_",""), true);
        }

        public List<Profile> Discover()
        {
            XField xthis = new(Path);
            XField? submacros = xthis.Test("connections");
            if (submacros == null) return [];
            return [.. submacros.Flatten("connection::macro::ref").Select(x => new Profile(x.Value, out _))];
        }
    }

    internal class Component : IReference
    {
        private WeakReference<XField>? _cached;

        public string Name { get; }
        public string Path { get; }
        public Macros Class { get; }
        public XField Property
        {
            get
            {
                if (_cached?.TryGetTarget(out XField? loaded) == true) return loaded;
                loaded = new XField(Path)["component"];
                _cached = new WeakReference<XField>(loaded);
                return loaded;
            }
        }

        public Component(Profile macro, out XField data)
        {
            Name = macro.Target;
            Path = EntryTracker.Tracker.Redirections[Name];
            data = new XField(Path);
            Class = Enum.Parse<Macros>(data["component::class"].Value.Replace("_", ""), true);

            /*
             Inconsistencies <components class, macro class>
               ShipL!=ShipXL (ship_xen_xl_destroyer_01_a_macro)
               Destructible!=Scanner (scanner_gen_s_longrange_01_mk1_macro)
               Destructible!=Scanner (scanner_gen_s_longrange_01_mk2_macro)
               Destructible!=Scanner (scanner_gen_s_mining_01_mk1_macro)
               Destructible!=Scanner (scanner_gen_s_mining_01_mk2_macro)
               Destructible!=Scanner (scanner_gen_s_object_01_mk1_macro)
               Destructible!=Scanner (scanner_gen_s_object_01_mk2_macro)
               Destructible!=Scanner (scanner_gen_s_object_01_mk3_macro)
               ShipXL!=ShipL (ship_ter_l_miner_liquid_01_a_macro)
               Connectionmodule!=Storage (landmarks_gen_piratestation_01_ring_01_macro)
            */
        }
    }
}
