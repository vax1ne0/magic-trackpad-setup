using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

internal static class TrayText
{
    static readonly Lazy<Dictionary<string,string>> Arabic = new Lazy<Dictionary<string,string>>(delegate {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Labels.xml"))
        {
            var language=XDocument.Load(stream).Root.Elements("Language").Single(e=>(string)e.Attribute("id")=="ar");
            return language.Elements("String").Where(e=>((string)e.Attribute("id")).StartsWith("Tray.",StringComparison.Ordinal))
                .ToDictionary(e=>((string)e.Attribute("id")).Substring(4),e=>e.Value);
        }
    });
    internal static bool IsArabic
    {
        get { return AppPreferences.ReadText("Language",CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)=="ar"; }
    }
    internal static string Get(string key,string fallback)
    {
        string value;
        return IsArabic && Arabic.Value.TryGetValue(key,out value) ? value : fallback;
    }
}
