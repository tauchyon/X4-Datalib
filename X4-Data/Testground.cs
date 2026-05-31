using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using X4Data;

namespace X4Extractor
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var testresult = X4Context.Build("F:\\SteamLibrary\\steamapps\\common\\X4 Foundations\\data");
            var testloc = Localization.Texts;
            var testtexts = testloc.GlobalPool.Values.SelectMany(s => s);
            Debugger.Break();
        }
    }
}
