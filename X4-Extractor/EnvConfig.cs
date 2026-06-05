// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace X4Extractor
{
    public enum GamePartition
    {   // do not rename : named by dlc assets dir
        main, split, terran, 
        pirate, boron, timelines,
        mini_01, mini_02,
        extension
    }   // do not reorder : ordered by release date

    public class EnvConfig
    {
        public const string DataPath = "libraries";
        public const string LangFileFormat = "t\\0001-l{0:D3}.xml";
        public const bool ImmediateText = true;

        public static readonly Dictionary<string, byte> I18nCode = new()
        {
            { "English", 44 }, { "German", 49 }, { "French", 33 },
            { "Italian", 39 }, { "Russian", 7 }, { "Spanish", 34 },
            { "Portuguese", 55 }, { "Polish", 48 }, { "Czech", 42 },
            { "Simplified Chinese", 86 }, { "Traditional Chinese", 88 },
            { "Korean", 82 }, { "Japanese", 81 }
        };

        private static EnvConfig? _appconf;
        private string _lang = "English";

        private EnvConfig(string datasource, string basegamepath) => (GamedataPath, BasegamePath) = (datasource, basegamepath);
        public static EnvConfig Config => _appconf ?? throw new NullReferenceException("EnvConfig is not initialized.");
        public string GamedataPath { get; init; }
        public string BasegamePath { get; init; }
        public string Language { get => _lang; set => OnLanguageChanged.Invoke(this, value); }

        public static EnvConfig Setup(string gamepath)
        {
            if(_appconf != null)
                throw new InvalidOperationException("EnvConfig is already initialized.");
            string basegame = new DirectoryInfo(gamepath).GetDirectories().First(d => d.GetFiles().Any(fi => fi.Name == "scriptproperties.html")).FullName;
            _appconf = new EnvConfig(gamepath, basegame);
            return _appconf;
        }

        public event EventHandler<string> OnLanguageChanged = (sender, target) =>
        {
            if (!I18nCode.ContainsKey(target))
                throw new ArgumentException($"Unsupported language: {target}");
            (sender as EnvConfig)!._lang = target;
        };
    }
}