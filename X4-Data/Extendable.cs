using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using X4Extractor;

namespace X4Data
{
    public enum Connections : sbyte { Engine, Thruster, Shield, Weapon, Turret }

    public enum Components : byte { Ship, Module, Misc, Engine, Thruster, Shield, Weapon, Turret, }

    public enum Extents : byte { Invalid, Extrasmall, Small, Medium, Large, ExtraLarge }

    public enum ComType : byte { Dedicated, Standard, Missile, Mining, Highpower, Venture, Boron, Xenon, Advanced }

    public record struct Slot
    {
        public string? Group { get; internal set; }
        public Connections Category { get; internal set; }
        public Extents Tier { get; internal set; }
        public ComType[] Types { get; internal set; }
        public HashSet<Tags> Detail { get; internal set; }
        public (Vector3 Pos, Quaternion Rot) Offset { get; internal set; }
    }

    internal class Extendable : IExtendable
    {
        object IExtendable.Source => Source; 
        public Product Source { get; init; }
        public List<Factions> Economy { get; internal set; }
        public Licenses? Restriction { get; internal set; }
        public Wares Id => Source.Id;
        public Transports Class => Source.Class;
        public Groups? Category => Source.Category;
        public uint Volume => Source.Volume;
        public (uint, uint, uint) Prices => Source.Prices;

        internal string Prefix { get; init; }
        public Components Type { get; init; }
        public Profile Property { get; init; }
        public Component Self { get; init; }
        public List<Slot> Extends { get; internal set; } = [];

        public HashSet<string>? Groups { get; internal set; }
        public Dictionary<string, List<Slot>>? Binds { get; internal set; }

        public Extendable(EndPoint site)
        {
            Source = site;
            Economy = site.Economy;
            Restriction = site.Restriction;

            Prefix = ((string)site.Id).Split('_')[0];
            Type = Enum.TryParse(Prefix, true, out Components type) ? type : Components.Misc;
            Property = new Profile(site, out XField macro);
            Self = new Component(Property, out XField data);

            if (Self.Class >= Macros.Storage) return;

            XField? connections = data.Route("component::connections");
            if(connections != null) Extends = [.. Distiller.Slots(connections)];
            else Debugger.Break();

            if (Type != Components.Ship && Type != Components.Module) return;
            Groups = [.. Extends.Where(s => s.Group != null).Select(s => s.Group!).Distinct()];
            Binds = Groups.ToDictionary(g => g, g => Extends.Where(s => s.Group == g).ToList());
        }

        public int Interchangeable(Extendable attach)
        {
            if (this.Extends.Count > 1) throw new InvalidOperationException();
            if (this.Extends[0].Types.Length > 1) Debugger.Break();
            if (attach.Type != Components.Ship && attach.Type != Components.Module)
                return this.Extends == attach.Extends ? 1 : 0;
            Slot self = this.Extends[0]; ComType type = self.Types[0];
            bool mandatory = this.Type == Components.Engine || this.Type == Components.Thruster || self.Detail.Contains("mandatory");
            if (!self.Types.Contains(ComType.Dedicated))
            {
                int compatibles = attach.Extends.Count(slot => slot.Types.Contains(type));
                return mandatory ? -compatibles : compatibles;
            }
            if (self.Detail.Count > 1) throw new InvalidDataException();
            int match = attach.Extends.Count(slot => slot.Detail.Contains(self.Detail.Single()));
            return mandatory ? -match : match;
        }
    }
}
