using System;
using System.IO;
using PdfSharp.Fonts;

namespace CbeMeetingAttendance.Services
{
    public class WindowsFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
        {
            if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
            {
                if (bold && italic)
                {
                    return new FontResolverInfo("arialbi.ttf");
                }
                if (bold)
                {
                    return new FontResolverInfo("arialbd.ttf");
                }
                if (italic)
                {
                    return new FontResolverInfo("ariali.ttf");
                }
                return new FontResolverInfo("arial.ttf");
            }
            return null;
        }

        public byte[]? GetFont(string faceName)
        {
            // Resolve path to the standard Windows Fonts folder
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var fontPath = Path.Combine(windowsFolder, "Fonts", faceName);

            if (File.Exists(fontPath))
            {
                return File.ReadAllBytes(fontPath);
            }

            // Fallback: search locally if they were copied
            var localFontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, faceName);
            if (File.Exists(localFontPath))
            {
                return File.ReadAllBytes(localFontPath);
            }

            return null;
        }
    }
}
