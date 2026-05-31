using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

namespace X4Extractor
{
    internal interface IEntry
    {
        public string Id { get; init; }
        public IEnumerable<(string Field, TextRef Ref)> TextRefs { get; }
    }

    internal readonly struct RaceEntry : IEntry
    {
        public string Id { get; init; }
        public TextRef Name { get; init; }
        public TextRef Desc { get; init; }
        public TextRef ShortName { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is RaceEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Desc),
            ("ShortName", ShortName)
        ];
    }

    internal readonly struct MethodEntry : IEntry
    {
        public string Id { get; init; }
        public string? Race { get; init; }
        public string[] Tags { get; init; }
        public TextRef Name { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is MethodEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name)
        ];
    }

    internal readonly struct FactionEntry : IEntry
    {
        public readonly struct LicenseEntry : IEntry
        {
            public string Id { get; init; }
            public TextRef Name { get; init; }

            public override int GetHashCode() => Id.GetHashCode();
            public override bool Equals([NotNullWhen(true)] object? obj) => obj is LicenseEntry entry && entry.Id == this.Id;

            public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
            [
                ("Name", Name)
            ];
        }

        public string Id { get; init; }
        public string Race { get; init; }
        public string[] Tags { get; init; }
        public HashSet<LicenseEntry> Licenses { get; init; }
        public TextRef Name { get; init; }
        public TextRef Description { get; init; }
        public TextRef ShortName { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is FactionEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Description),
            ("ShortName", ShortName)
        ];
    }

    internal readonly struct WareEntry : IEntry
    {
        public string Id { get; init; }
        public string Class { get; init; }
        public string? Category { get; init; }
        public string[] Tags { get; init; }
        public TextRef Name { get; init; }
        public TextRef Description { get; init; }

        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is WareEntry entry && entry.Id == this.Id;

        public IEnumerable<(string Field, TextRef Ref)> TextRefs =>
        [
            ("Name", Name),
            ("Description", Description)
        ];
    }

    internal class EntryTracker
    {
        private static EntryTracker? _tracker;
        private TextPool _site;

        public static EntryTracker Record => _tracker ?? throw new NullReferenceException("EntryTracker is not initialized.");
        public HashSet<RaceEntry> Races { get; } = [];
        public HashSet<MethodEntry> Methods { get; } = [];
        public HashSet<FactionEntry> Factions { get; } = [];
        public HashSet<WareEntry> Sources { get; } = [];
        public Dictionary<string, List<XElement>> Postreads { get; } = [];
        public bool Sealed { get; private set; } = false;

        private EntryTracker(TextPool langservice) => _site = langservice;

        public static EntryTracker Initialize()
        {
            if (_tracker != null)
                throw new InvalidOperationException("EntryTracker is already initialized.");
            _tracker = new EntryTracker(TextPool.Service);
            return _tracker;
        }

        public bool WriteRace(string id, TextRef name, TextRef desc, TextRef shortName)
        {   // Fundamental data, load immediately and directly
            _site.Load(name);
            _site.Load(desc);
            _site.Load(shortName);
            return Races.Add(new RaceEntry { Id = id, Name = name, Desc = desc, ShortName = shortName });
        }

        public bool AddMethod(MethodEntry entry) => Methods.Add(entry);

        public bool AddFaction(FactionEntry entry)
        {
            if (EnvConfig.ImmediateText)
            {
                _site.Load(entry.Name);
                _site.Load(entry.Description);
                _site.Load(entry.ShortName);
                foreach (FactionEntry.LicenseEntry license in entry.Licenses)
                    _site.Load(license.Name);
            }
            return Factions.Add(entry);
        }

        public bool AddWare(WareEntry entry)
        {
            if (EnvConfig.ImmediateText)
            {
                _site.Load(entry.Name);
                _site.Load(entry.Description);
            }
            return Sources.Add(entry);
        }

        public bool AddPostread(string id, string xmlcontent)
        {
            if (!Postreads.ContainsKey(id))
                Postreads[id] = [];
            Postreads[id].Add(XElement.Parse(xmlcontent));
            return true;
        }

        public static void Finish()
        {
            if (_tracker!.Sealed)
                throw new InvalidOperationException("EntryTracker is already sealed.");
            string[] ventures = [.. _tracker.Sources.Where(ware => ware.Id.Contains("_venture", StringComparison.OrdinalIgnoreCase)).Select(ware => ware.Id)];
            foreach (var drop in ventures)
            {
                _tracker.Sources.RemoveWhere(ware => ware.Id == drop);
                _tracker.Postreads.Remove(drop);
            }
            string[] afloats = [.. _tracker.Postreads.Keys.Where(id => _tracker.Sources.All(ware => ware.Id != id))];
#if DEBUG
            foreach (string id in afloats)
                Console.WriteLine($"[WARN] Postread without source entry: {id} ({_tracker.Postreads[id][0]}) (Do DLC data Missing?)");
#endif
            foreach (string id in afloats)
                _tracker.Postreads.Remove(id);
            _tracker.Sealed = true;
        }

        public void DumpEntry()
        {
            throw new NotImplementedException();
        }

        public HashSet<string> AccRaces() => [.. Races.Select(r => r.Id)];
        public HashSet<string> AccMethods() => [.. Methods.Select(m => m.Id)];
        public HashSet<string> AccFactions() => [.. Factions.Select(f => f.Id)];
        public HashSet<string> AccLicences() => [.. Factions.SelectMany(fac => fac.Licenses).Select(l => l.Id)];

        public HashSet<string> AccWares() => [.. Sources.Select(s => s.Id)];

        public HashSet<string> AccTransports() => [.. Sources.Select(w => w.Class).Distinct()];
        public HashSet<string> AccGroups() => [.. Sources.Select(w => w.Category).Where(c => c != null).Distinct()!];
        public HashSet<string> AccTags() => [.. Sources.SelectMany(w => w.Tags).Distinct()];
    }

    internal class XmlExtractor
    {
        private static readonly XmlReaderSettings Setting = new() { IgnoreWhitespace = true };

        private readonly EntryTracker _tracker; 
        private XmlReader _reader;

        public readonly bool Basegame;
        public readonly string Partition;
        public FileInfo WaresFile { get; init; }
        public FileInfo FactionsFile { get; init; }

        public XmlExtractor(EnvConfig config, EntryTracker tracker, string partition)
        {
            Partition = partition;
            _tracker = tracker;
            WaresFile = new FileInfo(Path.Combine(config.GamedataPath, partition, EnvConfig.DataPath, "wares.xml"));
            FactionsFile = new FileInfo(Path.Combine(config.GamedataPath, partition, EnvConfig.DataPath, "factions.xml"));
            Basegame = new FileInfo(Path.Combine(config.GamedataPath, partition, EnvConfig.DataPath, "races.xml")).Exists;
        }

        public static void Extract(string partition)
            => new XmlExtractor(EnvConfig.Config, EntryTracker.Record, partition).Extract();

        public void Extract()
        {
            if (Basegame) using (_reader = XmlReader.Create(Path.Combine(EnvConfig.Config.BasegamePath, EnvConfig.DataPath, "races.xml"), Setting))
                if (XDocument.Load(_reader).Element("races")!.Descendants("race")
                    .Where(race => race.Attribute("tags") is not { Value: "hidden" })
                    .Select(e =>
                        _tracker.WriteRace(
                            id: e.Attribute("id")!.Value,
                            name: (TextRef)e.Attribute("name")!.Value,
                            desc: (TextRef)e.Attribute("description")!.Value,
                            shortName: (TextRef)e.Attribute("shortname")!.Value)
                    ).Any(b => !b))
                    throw new ApplicationException("Critical context error");
            if (FactionsFile.Exists) using (_reader = XmlReader.Create(FactionsFile.FullName, Setting))
                LoadFactions(Basegame ? XDocument.Load(_reader).Element("factions")!
                    : XDocument.Load(_reader).Element("diff")!.Elements("add").Last(e => e.Attribute("sel")?.Value == "/factions"));
            using (_reader = XmlReader.Create(WaresFile.FullName, Setting))
            {
                _reader.MoveToContent();
                if (Basegame) ExtractBasegame();
                else ExtractExtension();
            }
        }

        private void ScanMethods()
        {
            while (_reader.NodeType != XmlNodeType.EndElement)
            {
                if (_reader.IsEmptyElement)
                {
                    _tracker.AddMethod(new()
                    {
                        Id = _reader.GetAttribute("id") ?? throw new InvalidDataException("Missing id attribute"),
                        Tags = _reader.GetAttribute("tags")?.Split(' ') ?? [],
                        Name = (TextRef)(_reader.GetAttribute("name") ?? throw new InvalidDataException("Missing name attribute"))
                    });
                    _reader.Skip();
                    continue;
                }
                string id = _reader.GetAttribute("id") ?? throw new InvalidDataException("Missing id attribute");
                string[] tags = _reader.GetAttribute("tags")?.Split(' ') ?? [];
                string name = _reader.GetAttribute("name") ?? throw new InvalidDataException("Missing name attribute");
                _reader.Read(); /*<default>*/

                _tracker.AddMethod(new()
                {
                    Id = id,
                    Tags = tags,
                    Name = (TextRef)name,
                    Race = _reader.GetAttribute("race") ?? throw new InvalidDataException("Missing race attribute")
                });
                _reader.Read(); /*</method>*/
                _reader.Read(); /*<method>*/
            }
        }

        private void LoadFactions(XElement facdecl)
        {
            IEnumerable<XElement> factions = facdecl.Descendants("faction")
                .Where(fac => fac.Attribute("id")?.Value != "player" && !fac.Attribute("tags")!.Value.Contains("hidden"));
            foreach (XElement elem in factions)
            {
                HashSet<FactionEntry.LicenseEntry> license =
                [
                    .. (elem.Element("licences") ?? new XElement("licences")).Elements("licence")
                    .Where(e => e.Attribute("name") != null)
                    .Select(e => new FactionEntry.LicenseEntry
                    {
                        Id = e.Attribute("type")?.Value ??
                             throw new InvalidDataException("Missing type attribute in licence element"),
                        Name = (TextRef)e.Attribute("name")!.Value
                    })
                ];
                bool success = _tracker.AddFaction(new FactionEntry
                {
                    Id = elem.Attribute("id")?.Value ?? throw new InvalidDataException("Missing id attribute in faction element"),
                    Race = elem.Attribute("primaryrace")?.Value ?? throw new InvalidDataException("Regular faction should have primary race"),
                    Tags = elem.Attribute("tags")?.Value.Split(' ') ?? [],
                    Name = (TextRef)(elem.Attribute("name")?.Value ?? throw new InvalidDataException("Missing name attribute in faction element")),
                    Description = (TextRef)(elem.Attribute("description")?.Value ?? throw new InvalidDataException("Regular faction should have description")),
                    ShortName = (TextRef)(elem.Attribute("shortname")?.Value ?? throw new InvalidDataException("Regular faction should have shortname")),
                    Licenses = license
                });
                Debug.Assert(success);
            }
        }

        private void ScanWares(string methodoverride = "default")
        {
            while (_reader.NodeType != XmlNodeType.EndElement && _reader.NodeType != XmlNodeType.None)
            {
                WareEntry entry = new()
                {
                    Id = _reader.GetAttribute("id") ?? throw new InvalidDataException("Missing id attribute"),
                    Class = _reader.GetAttribute("transport") ?? throw new InvalidDataException("Missing class attribute"),
                    Category = _reader.GetAttribute("group"),
                    Tags = _reader.GetAttribute("tags")?.Split(' ') ?? [],
                    Name = (TextRef)((_reader.GetAttribute("name") ?? throw new InvalidDataException("Missing name attribute")).Contains(',') ? _reader.GetAttribute("name")! : "{20224,1}"),
                    Description = (TextRef)(_reader.GetAttribute("description") ?? TextRef.Invalid),
                };
                bool unique = _tracker.AddWare(entry);
                Debug.Assert(unique);
                _tracker.AddPostread(entry.Id, _reader.ReadOuterXml().Replace("default", methodoverride));
            }
        }

        private void ExtractBasegame()
        {
            _reader.ReadToFollowing("method");
            ScanMethods();

            _reader.ReadToFollowing("ware");
            ScanWares();
        }

        private void ExtractExtension()
        {
            /* For DLCs
             * XML elements of extensions are all <add> that modify the base game data (table)
             * Here are three kind of them: (sort by the strict arrangement in the XML file)
             * - Method declaration : Sub node of <add sel="/wares/production">
             * - New wares : Sub node of <add sel="/wares">
             * - Additional production : <add sel="/wares/ware[@id='targetwareid']">
             */

            Debug.Assert(_reader.Name == "diff");
            _reader.Read();
            if (_reader.GetAttribute("sel") == "/wares/production")
            {
                _reader.ReadToFollowing("method");
                ScanMethods();
                _reader.Skip();
            }
            if (_reader.GetAttribute("sel") == "/wares")
            {
                _reader.ReadToFollowing("ware");
                ScanWares();
                _reader.Skip();
            }
            while (_reader.NodeType != XmlNodeType.EndElement && _reader.NodeType != XmlNodeType.None)
            {
                string path = _reader.GetAttribute("sel") ?? throw new InvalidDataException("Missing sel attribute");
                _tracker.AddPostread(path[17..^2], _reader.ReadOuterXml());
            }
        }
    }
}
