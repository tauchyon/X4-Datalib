using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace X4Extractor
{
    public static class X4Wares
    {
        public static Extractor Initbase(string entrypath)
        {
            EnvConfig.Setup(entrypath);
            TextPool.Initialize();

            EntryTracker.Initialize();

            DirectoryInfo datadir = new(EnvConfig.Config.GamedataPath);
            foreach (var gamedir in datadir.GetDirectories())
                XmlExtractor.Extract(gamedir.Name);
            EntryTracker.Finish();

            Localization.Initialize();

            var extractor = new Extractor();
            extractor.Extract();
            return extractor;
        }
    }
}
