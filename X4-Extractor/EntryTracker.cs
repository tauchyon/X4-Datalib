using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace X4Extractor
{
    internal interface IEntry
    {
        internal string Source { get; }

        public string Id { get; init; }
        public IEnumerable<(string Field, TextRef Ref)> TextRefs { get; }
    }

    internal readonly struct RaceEntry : IEntry
    {
        [AllowNull] public string Source { get; internal init; }

        public string Id { get; init; }
        public TextRef Name { get; init; }
        public TextRef Description { get; init; }
        public TextRef ShortName { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object? obj) => obj is RaceEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Description),
            ("ShortName", ShortName)
        ];
    }

    internal readonly struct MethodEntry : IEntry
    {
        public string Source { get; internal init; }

        public string Id { get; init; }
        public string? Race { get; init; }
        public string[] Tags { get; init; }
        public TextRef Name { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object? obj) => obj is MethodEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name)
        ];
    }

    internal readonly struct FactionEntry : IEntry
    {
        public readonly struct LicenseEntry : IEntry
        {
            public string Source { get; internal init; }
            
            public string Id { get; init; }
            public TextRef Name { get; init; }

            public override int GetHashCode() => Id.GetHashCode();
            public override bool Equals(object? obj) => obj is LicenseEntry entry && entry.Id == this.Id;

            public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
            [
                ("Name", Name)
            ];
        }

        public string Source { get; internal init; }

        public string Id { get; init; }
        public string Race { get; init; }
        public string[] Tags { get; init; }
        public HashSet<LicenseEntry> Licenses { get; init; }
        public TextRef Name { get; init; }
        public TextRef Description { get; init; }
        public TextRef ShortName { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object? obj) => obj is FactionEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Description),
            ("ShortName", ShortName)
        ];
    }

    internal readonly struct WareEntry : IEntry
    {
        public string Source { get; internal init; }

        public string Id { get; init; }
        public string Transport { get; init; }
        public string? Group { get; init; }
        public string[] Tags { get; init; }
        public TextRef Name { get; init; }
        public TextRef Description { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object? obj) => obj is WareEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Description)
        ];
    }

    internal readonly struct GroupEntry : IEntry
    {
        [AllowNull] public string Source { get; internal init; }

        public string Id { get; init; }
        public int? Tier { get; init; }
        public TextRef Name { get; init; }

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name)
        ];
    }

    internal class EntryTracker
    {
        private static readonly string[] FetchGroups = ["ship", "module", "station"];

        private readonly TextPool _site;
        private static EntryTracker? _dependency;
        
        private string _register;
        private bool _solid = false;
        private readonly List<string> _afloats = [];

        public bool Sealed { get; private set; } = false;
        public readonly Dictionary<string, string> Redirections = [];
        public readonly Dictionary<string, List<string>> Collections = [];
        public readonly Dictionary<string, Gamepart> Partitions = [];

        public HashSet<FactionEntry.LicenseEntry> Licenses => [.. Factions.SelectMany(f => f.Licenses)];
        public HashSet<RaceEntry> Races { get; private set; }
        public HashSet<MethodEntry> Methods { get; private set; }
        public HashSet<FactionEntry> Factions { get; private set; }
        public HashSet<WareEntry> Wares { get; private set; }
        public HashSet<GroupEntry> Groups { get; private set; }
        public List<XElement> Postreads { get; private set; } = [];

        private readonly Dictionary<string, XElement> _definition = [];
        private readonly Dictionary<string, List<XElement>> _merging = [];

        private EntryTracker(TextPool langservice) => _site = langservice;
        public static EntryTracker Tracker => _dependency ?? throw new InvalidOperationException("EntryTracker is not initialized");
        public static EntryTracker Initialize() 
        {
            if (_dependency != null) throw new InvalidOperationException("EntryTracker is already initialized");
            _dependency = new EntryTracker(TextPool.Service);
            return Tracker;
        }

        public void Tracking(string path)
        {
            Debug.Assert(!Sealed); _register = path;
            DirectoryInfo workspace = new(path);
            if (workspace.GetFiles().Any(fi => fi.Name.Contains(".html")))
            {
                Maingame(path);
                Remapping(workspace.FullName);
                _afloats.ForEach(Tracking); _afloats.Clear();
            }
            else
            {
                Remapping(workspace.FullName);
                if (_solid) ExtDiff(path);
                else _afloats.Add(path);
            }
        }

        private void Maingame(string foundation)
        {
            Partitions[foundation] = Gamepart.main; _solid = true;
            Races = [.. XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "races.xml")).Root!.Elements("race")
                .Where(xr => xr.Attribute("tags")?.Value.Contains("hidden") is not true)
                .Select(XRace) ];
            Methods = [.. XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "wares.xml")).Root!.Element("production")!
                .Elements("method")
                .Select(XMethod) ];
            Factions = [.. XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "factions.xml")).Root!.Elements("faction")
                .Where(xf => xf.Attribute("id")!.Value != "player" && xf.Attribute("tags")?.Value.Contains("hidden") is not true)
                .Select(XFaction) ];
            Wares = [.. XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "wares.xml")).Root!.Elements("ware")
                .Select(XWare) ];
            Groups = [.. XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "waregroups.xml")).Root!
                .Elements("group")
                .Select(xg => new GroupEntry {
                        Id = xg.Attribute("id")!.Value,
                        Name = (TextRef)xg.Attribute("name")!.Value,
                        Tier = int.Parse(xg.Attribute("tier")?.Value ?? "0")
                    }
                )
            ];
            foreach (XElement xware in XDocument.Load(Path.Combine(foundation, EnvConfig.DataPath, "wares.xml")).Root!.Elements("ware"))
                Future(xware);
        }

        private bool? Future(XElement xware)
        {
            void DeclWare(XElement xtar)
            {
                if (xtar.Attribute("illegal") != null)
                {
                    string swap = xtar.Attribute("illegal")!.Value;
                    xtar.SetAttributeValue("illegal", null);
                    xtar.Add(new XElement("illegal", new XAttribute("factions", swap)));
                }
                xtar.SetAttributeValue("_source", Partitions[_register]);
                foreach (XElement xprod in xtar.Elements("production"))
                    xprod.SetAttributeValue("_source", Partitions[_register]);
                // Faction sources contained in FactionEntry, needn't remark here
            }

            void MergeWare(XElement xtar, XElement xmod)
            {
                XElement? xillegal = xmod.Element("illegal");
                if (xillegal is not null && xtar.Element("illegal") is not null)
                {
                    xtar.Element("illegal")!.Value = xtar.Element("illegal")!.Value + " " + xillegal.Value;
                    xillegal.Remove();
                }
                foreach (XElement xprod in xmod.Elements("production"))
                    xprod.SetAttributeValue("_source", Partitions[_register]);
                xtar.Add(xmod.Elements());
            }

            if (xware.Name == "ware")
            {
                if (_definition.ContainsKey(xware.Attribute("id")!.Value)) throw new InvalidDataException("Definition collided");
                string declid = xware.Attribute("id")!.Value;
                DeclWare(xware);
                Postreads.Add(xware);
                _definition[declid] = xware;
                if (!_merging.TryGetValue(declid, out List<XElement>? afloats)) return true;
                foreach (XElement xmod in afloats)
                    MergeWare(_definition[declid], xmod);
                _merging.Remove(declid);
                return true;
            }

            string baseid = xware.Attribute("sel")!.Value[17..^2];
            if (!_definition.TryGetValue(baseid, out XElement? @base))
            {
                if(!_merging.ContainsKey(baseid))
                    _merging[baseid] = [];
                _merging[baseid].Add(xware);
                return null;
            }
            MergeWare(@base, xware);
            return true;
        }

        private RaceEntry XRace(XElement xrace) => new()
        {
            Id = xrace.Attribute("id")!.Value,
            Name = (TextRef)xrace.Attribute("name")!.Value,
            Description = (TextRef)xrace.Attribute("description")!.Value,
            ShortName = (TextRef)xrace.Attribute("shortname")!.Value,
        };

        private MethodEntry XMethod(XElement xmethod) => new()
        {
            Source = _register,
            Id = xmethod.Attribute("id")!.Value,
            Name = (TextRef)xmethod.Attribute("name")!.Value,
            Race = xmethod.Element("default")?.Attribute("race")!.Value,
            Tags = [.. xmethod.Attribute("tags")?.Value.Split(',').Select(s => s.Trim()) ?? []]
        };

        private FactionEntry XFaction(XElement xfaction) => new()
        {
            Source = _register,
            Id = xfaction.Attribute("id")!.Value,
            Race = xfaction.Attribute("primaryrace")!.Value,
            Name = (TextRef)xfaction.Attribute("name")!.Value,
            Description = (TextRef)xfaction.Attribute("description")!.Value,
            ShortName = (TextRef)xfaction.Attribute("shortname")!.Value,
            Tags = xfaction.Attribute("tags")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [],
            Licenses = [.. xfaction.Element("licences")?.Elements("licence")
                .Where(xl=> xl.Attribute("name") is not null)
                .Select(xl=> new FactionEntry.LicenseEntry() {
                    Source = xfaction.Attribute("id")!.Value,
                    Id = xl.Attribute("type")!.Value,
                    Name = (TextRef)xl.Attribute("name")!.Value
                }) ?? []]
        };

        private WareEntry XWare(XElement xware) => new()
        {
            Source = _register,
            Id = xware.Attribute("id")!.Value,
            Transport = xware.Attribute("transport")!.Value,
            Group = xware.Attribute("group")?.Value,
            Name = (TextRef)xware.Attribute("name")!.Value,
            Description = (TextRef)(xware.Attribute("description")?.Value ?? TextRef.Invalid),
            Tags = xware.Attribute("tags")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? []
        };

        private int Remapping(string local)
        {
            string Redirect(string url) =>
                !url.Contains("extensions\\ego_dlc_") ? url : string.Join('\\',url.Substring(19).Split('\\')[1..]);

            XElement redirectory = XDocument.Load(Path.Combine(local, "index", "macros.xml")).Root!;
            redirectory.Add(XDocument.Load(Path.Combine(local, "index", "components.xml")).Root!.Elements());
            if (!Partitions.ContainsKey(local))
                Partitions[local] = Enum.Parse<Gamepart>(redirectory.Elements("entry")
                    .First(e => e.Attribute("value")!.Value.Contains("extensions\\ego_dlc_"))
                    .Attribute("value")!.Value.Substring(19).Split('\\')[0]);
            foreach (XElement dict in redirectory.Elements("entry"))
                Redirections[dict.Attribute("name")!.Value] = Path.Combine(local, Redirect(dict.Attribute("value")!.Value) + ".xml");
            foreach (string lookup in FetchGroups)
            {
                string groupxml = Path.Combine(local, EnvConfig.DataPath, lookup + "groups.xml");
                if(!File.Exists(groupxml)) continue;
                XElement xgroupcol = XDocument.Load(groupxml).Root!;
                foreach (XElement xgroup in xgroupcol.Elements("group"))
                    Collections[xgroup.Attribute("name")!.Value] =
                        [.. xgroup.Elements("select").Select(xs => xs.Attributes().First().Value)];
            }
            return redirectory.Elements().Count();
        }

        private void ExtDiff(string local)
        {

            List<XElement> Lookup(List<XElement> xadds) => [..
                xadds.Where(xa => xa.Attribute("sel")!.Value == "/wares").Elements("ware")
                    .Concat(xadds.Where(xa => xa.Attribute("sel")!.Value.Contains("/wares/ware[@id=\'")))];

            string facxml = Path.Combine(local, EnvConfig.DataPath, "factions.xml");
            if (File.Exists(facxml))
                Factions.UnionWith(XDocument.Load(facxml).Root!.Elements("add")
                    .Last(xa => xa.Attribute("sel")!.Value == "/factions")
                    .Elements("faction").Select(XFaction));
            Methods.UnionWith(XDocument.Load(Path.Combine(local, EnvConfig.DataPath, "wares.xml")).Root!.Elements("add")
                .Where(xa => xa.Attribute("sel")!.Value == "/wares/production")
                .Elements("method").Select(XMethod));
            Wares.UnionWith(XDocument.Load(Path.Combine(local, EnvConfig.DataPath, "wares.xml")).Root!.Elements("add")
                .Where(xa => xa.Attribute("sel")!.Value == "/wares")
                .Elements("ware").Select(XWare));
            foreach (XElement xware in Lookup([.. XDocument.Load(Path.Combine(local, EnvConfig.DataPath, "wares.xml")).Root!.Elements("add")]))
                Future(xware);
        }

        internal void Flush() => Flush(out _, out _, out _, out _, out _, out _);
        internal void Flush(
            out HashSet<RaceEntry> races,
            out HashSet<MethodEntry> methods,
            out HashSet<FactionEntry> factions,
            out HashSet<WareEntry> wares,
            out HashSet<GroupEntry> groups,
            out List<XElement> postreads)
        {
            Debug.Assert(!Sealed); Sealed = true;
            races = Races; Races = [];
            methods = Methods; Methods = [];
            factions = Factions; Factions = [];
            wares = Wares; Wares = [];
            groups = Groups; Groups = [];
            postreads = Postreads; Postreads = [];
            _definition.Clear(); if (_merging.Count > 0)
                throw new DataException("Unprocessed merging modification remain");
        }

        public HashSet<string> AccRaces() => [.. Races.Select(re => re.Id)];
        public HashSet<string> AccMethods() => [.. Methods.Select(me => me.Id)];
        public HashSet<string> AccFactions() => [.. Factions.Select(fe => fe.Id)];
        public HashSet<string> AccWares() => [.. Wares.Select(we => we.Id)];
        public HashSet<string> AccTransports() => [.. Wares.Select(we => we.Transport)];
        public HashSet<string> AccGroups() => [.. Groups.Select(ge => ge.Id)];
        public HashSet<string> AccLicences() => [.. Factions.SelectMany(fe => fe.Licenses).Select(le => le.Id).Distinct()];
        public HashSet<string> AccTags()
        {
            HashSet<string> result = [];
            result.UnionWith(Methods.SelectMany(me => me.Tags));
            result.UnionWith(Factions.SelectMany(fe => fe.Tags));
            result.UnionWith(Wares.SelectMany(we => we.Tags));
            return result;
        }
    }
}
