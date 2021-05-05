using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.IO;

namespace NadekoBot.Core.Services.Impl
{
    public class FontProvider : INService
    {
        private readonly FontCollection _fonts;

        public FontProvider()
        {
            _fonts = new FontCollection();

            NotoSans = _fonts.Install("data/fonts/NotoSans-Bold.ttf");
            UniSans = _fonts.Install("data/fonts/Uni Sans.ttf");

            FallBackFonts = new List<FontFamily>();

            //FallBackFonts.Add(_fonts.Install("data/fonts/OpenSansEmoji.ttf"));

            // try loading some emoji and jap fonts on windows as fallback fonts
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    string fontsfolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
                    FallBackFonts.Add(_fonts.Install(Path.Combine(fontsfolder, "seguiemj.ttf")));
                    FallBackFonts.AddRange(_fonts.InstallCollection(Path.Combine(fontsfolder, "msgothic.ttc")));
                    FallBackFonts.AddRange(_fonts.InstallCollection(Path.Combine(fontsfolder, "segoe.ttc")));
                }
                catch { }
            }

            // any fonts present in data/fonts should be added as fallback fonts
            // this will allow support for special characters when drawing text
            foreach (var font in Directory.GetFiles(@"data/fonts"))
            {
                if (font.EndsWith(".ttf"))
                {
                    FallBackFonts.Add(_fonts.Install(font));
                }
                else if (font.EndsWith(".ttc"))
                {
                    FallBackFonts.AddRange(_fonts.InstallCollection(font));
                }
            }

            RipFont = NotoSans.CreateFont(20, FontStyle.Bold);
        }

        public FontFamily UniSans { get; }
        public FontFamily NotoSans { get; }
        //public FontFamily Emojis { get; }

        /// <summary>
        /// Font used for .rip command
        /// </summary>
        public Font RipFont { get; }
        public List<FontFamily> FallBackFonts { get; }
    }
}
