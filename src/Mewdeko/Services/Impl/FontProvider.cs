using System.IO;
using SkiaSharp;

namespace Mewdeko.Services.Impl;

public class FontProvider
{
    public FontProvider()
    {
        var fontsFolder = "data/fonts";
        NotoSans = SKTypeface.FromFile(Path.Combine(fontsFolder, "NotoSans-Bold.ttf"));
        UniSans = SKTypeface.FromFile(Path.Combine(fontsFolder, "Uni Sans.ttf"));

        FallBackFonts = new List<SKTypeface>();

        // Try loading some fallback fonts
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            try
            {
                var systemFontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                FallBackFonts.Add(SKTypeface.FromFile(Path.Combine(systemFontsFolder, "seguiemj.ttf")));
                FallBackFonts.Add(SKTypeface.FromFile(Path.Combine(systemFontsFolder, "msgothic.ttc")));
            }
            catch
            {
                // ignored
            }
        }

        foreach (var font in Directory.GetFiles(fontsFolder))
        {
            if (font.EndsWith(".ttf") || font.EndsWith(".ttc"))
                FallBackFonts.Add(SKTypeface.FromFile(font));
        }

        RipFont = new SKPaint
        {
            Typeface = NotoSans, TextSize = 20, IsAntialias = true,
        };
    }

    public SKTypeface UniSans { get; }

    public SKTypeface NotoSans { get; }

    /// <summary>
    ///     Font used for .rip command
    /// </summary>
    public SKPaint RipFont { get; }

    public List<SKTypeface> FallBackFonts { get; }
}