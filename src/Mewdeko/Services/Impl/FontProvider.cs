using System.IO;
using SkiaSharp;

namespace Mewdeko.Services.Impl;

/// <summary>
/// Provides fonts for the application.
/// </summary>
public class FontProvider
{
    /// <summary>
    /// Initializes a new instance of the FontProvider class.
    /// </summary>
    public FontProvider()
    {
        var fontsFolder = "data/fonts";
        NotoSans = SKTypeface.FromFile(Path.Combine(fontsFolder, "NotoSans-Bold.ttf"));
        UniSans = SKTypeface.FromFile(Path.Combine(fontsFolder, "Uni Sans.ttf"));

        FallBackFonts = [];

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

    /// <summary>
    /// Gets the UniSans font.
    /// </summary>
    public SKTypeface UniSans { get; }

    /// <summary>
    /// Gets the NotoSans font.
    /// </summary>
    public SKTypeface NotoSans { get; }

    /// <summary>
    /// Gets the font used for the .rip command.
    /// </summary>
    public SKPaint RipFont { get; }

    /// <summary>
    /// Gets the list of fallback fonts.
    /// </summary>
    public List<SKTypeface> FallBackFonts { get; }
}