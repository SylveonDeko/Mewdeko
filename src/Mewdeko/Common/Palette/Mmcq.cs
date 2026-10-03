namespace Mewdeko.Common.Palette;

/// <summary>
///     A line by line port of the modified median cut quantizer (MMCQ) in <c>@lokesh.dhakar/quantize</c> 1.4.0, the
///     copy ColorThief 2.6 bundles and the web dashboard runs on every guild icon.
/// </summary>
/// <remarks>
///     Every quirk is kept so an icon quantizes to the same colors, in the same order, as it does in the browser: the
///     <c>else if</c> min/max scan, the truncating casts, the empty boxes a single cell split can produce, the stable
///     priority queue ordering, and the short circuit that returns the raw unique colors when there are few enough.
/// </remarks>
public static class Mmcq
{
    private const int Sigbits = 5;
    private const int Rshift = 8 - Sigbits;
    private const int MaxIterations = 1000;
    private const double FractByPopulations = 0.75;
    private const int Mult = 1 << (8 - Sigbits);

    /// <summary>
    ///     ColorThief's default sampling step: every 10th pixel.
    /// </summary>
    public const int DefaultQuality = 10;

    /// <summary>
    ///     ColorThief's <c>createPixelArray</c>: walks every <paramref name="quality" />th pixel and keeps it when its
    ///     alpha is at least 125 and it is not near white.
    /// </summary>
    /// <param name="pixels">Unpremultiplied pixels.</param>
    /// <param name="quality">The sampling step.</param>
    /// <returns>The sampled colors as <c>[r, g, b]</c>.</returns>
    public static List<int[]> SamplePixels(ReadOnlySpan<SkiaSharp.SKColor> pixels, int quality = DefaultQuality)
    {
        var output = new List<int[]>();
        for (var i = 0; i < pixels.Length; i += quality)
        {
            var c = pixels[i];
            if (c.Alpha >= 125 && !(c.Red > 250 && c.Green > 250 && c.Blue > 250))
                output.Add([c.Red, c.Green, c.Blue]);
        }

        return output;
    }

    /// <summary>
    ///     Quantizes <paramref name="pixels" /> to at most <paramref name="maxColors" /> colors.
    /// </summary>
    /// <param name="pixels">Colors as <c>[r, g, b]</c>.</param>
    /// <param name="maxColors">The color limit, from 2 to 256.</param>
    /// <returns>The palette in the order <c>CMap.palette()</c> yields it, or null when there are no pixels.</returns>
    public static List<int[]>? Quantize(List<int[]> pixels, int maxColors)
    {
        if (pixels.Count == 0 || maxColors is < 2 or > 256)
            return null;

        var seen = new HashSet<int>();
        var unique = new List<int[]>();
        foreach (var pixel in pixels)
        {
            if (seen.Add((pixel[0] << 16) | (pixel[1] << 8) | pixel[2]))
                unique.Add(pixel);
        }

        if (unique.Count <= maxColors)
            return unique.Select(p => (int[])p.Clone()).ToList();

        var histo = GetHisto(pixels);
        var vbox = VboxFromPixels(pixels, histo);
        var pq = new PQueue<VBox>((a, b) => a.Count().CompareTo(b.Count()));
        pq.Push(vbox);

        Iter(pq, histo, FractByPopulations * maxColors);

        var pq2 = new PQueue<VBox>((a, b) => (a.Count() * a.Volume()).CompareTo(b.Count() * b.Volume()));
        while (pq.Size > 0)
            pq2.Push(pq.Pop());

        Iter(pq2, histo, maxColors);

        var palette = new List<int[]>();
        while (pq2.Size > 0)
            palette.Add((int[])pq2.Pop().Avg().Clone());
        return palette;
    }

    private static int ColorIndex(int r, int g, int b)
    {
        return (r << (2 * Sigbits)) + (g << Sigbits) + b;
    }

    private static int At(int[] histo, int index)
    {
        return index >= 0 && index < histo.Length ? histo[index] : 0;
    }

    private sealed class PQueue<T>(Comparison<T> comparison)
    {
        private List<T> contents = [];
        private bool sorted;

        public int Size => contents.Count;

        public void Push(T item)
        {
            contents.Add(item);
            sorted = false;
        }

        public T Pop()
        {
            if (!sorted)
            {
                contents = contents.OrderBy(x => x, Comparer<T>.Create(comparison)).ToList();
                sorted = true;
            }

            var last = contents[^1];
            contents.RemoveAt(contents.Count - 1);
            return last;
        }
    }

    private sealed class VBox(int r1, int r2, int g1, int g2, int b1, int b2, int[] histo)
    {
        private long volumeCache;
        private long? countCache;
        private int[]? avgCache;

        public int R1 = r1, R2 = r2, G1 = g1, G2 = g2, B1 = b1, B2 = b2;

        public long Volume()
        {
            if (volumeCache == 0)
                volumeCache = (long)(R2 - R1 + 1) * (G2 - G1 + 1) * (B2 - B1 + 1);
            return volumeCache;
        }

        public long Count()
        {
            if (countCache is { } cached)
                return cached;
            long npix = 0;
            for (var i = R1; i <= R2; i++)
            for (var j = G1; j <= G2; j++)
            for (var k = B1; k <= B2; k++)
                npix += At(histo, ColorIndex(i, j, k));
            countCache = npix;
            return npix;
        }

        public VBox Copy()
        {
            return new VBox(R1, R2, G1, G2, B1, B2, histo);
        }

        public int[] Avg()
        {
            if (avgCache is not null)
                return avgCache;
            if (R1 == R2 && G1 == G2 && B1 == B2)
                return avgCache = [R1 << Rshift, G1 << Rshift, B1 << Rshift];

            long ntot = 0;
            double rsum = 0, gsum = 0, bsum = 0;
            for (var i = R1; i <= R2; i++)
            for (var j = G1; j <= G2; j++)
            for (var k = B1; k <= B2; k++)
            {
                var hval = At(histo, ColorIndex(i, j, k));
                ntot += hval;
                rsum += hval * (i + 0.5) * Mult;
                gsum += hval * (j + 0.5) * Mult;
                bsum += hval * (k + 0.5) * Mult;
            }

            return avgCache = ntot != 0
                ? [(int)(rsum / ntot), (int)(gsum / ntot), (int)(bsum / ntot)]
                :
                [
                    (int)(Mult * (R1 + R2 + 1) / 2.0), (int)(Mult * (G1 + G2 + 1) / 2.0),
                    (int)(Mult * (B1 + B2 + 1) / 2.0)
                ];
        }

        public int Lo(char dim)
        {
            return dim switch { 'r' => R1, 'g' => G1, _ => B1 };
        }

        public int Hi(char dim)
        {
            return dim switch { 'r' => R2, 'g' => G2, _ => B2 };
        }

        public void SetLo(char dim, int value)
        {
            switch (dim)
            {
                case 'r': R1 = value; break;
                case 'g': G1 = value; break;
                default: B1 = value; break;
            }
        }

        public void SetHi(char dim, int value)
        {
            switch (dim)
            {
                case 'r': R2 = value; break;
                case 'g': G2 = value; break;
                default: B2 = value; break;
            }
        }
    }

    private static int[] GetHisto(List<int[]> pixels)
    {
        var histo = new int[1 << (3 * Sigbits)];
        foreach (var pixel in pixels)
            histo[ColorIndex(pixel[0] >> Rshift, pixel[1] >> Rshift, pixel[2] >> Rshift)]++;
        return histo;
    }

    private static VBox VboxFromPixels(List<int[]> pixels, int[] histo)
    {
        int rmin = 1000000, rmax = 0, gmin = 1000000, gmax = 0, bmin = 1000000, bmax = 0;
        foreach (var pixel in pixels)
        {
            var rval = pixel[0] >> Rshift;
            var gval = pixel[1] >> Rshift;
            var bval = pixel[2] >> Rshift;
            if (rval < rmin) rmin = rval;
            else if (rval > rmax) rmax = rval;
            if (gval < gmin) gmin = gval;
            else if (gval > gmax) gmax = gval;
            if (bval < bmin) bmin = bval;
            else if (bval > bmax) bmax = bval;
        }

        return new VBox(rmin, rmax, gmin, gmax, bmin, bmax, histo);
    }

    private static List<VBox> MedianCutApply(int[] histo, VBox vbox)
    {
        var rw = vbox.R2 - vbox.R1 + 1;
        var gw = vbox.G2 - vbox.G1 + 1;
        var bw = vbox.B2 - vbox.B1 + 1;
        var maxw = Math.Max(rw, Math.Max(gw, bw));
        if (vbox.Count() == 1)
            return [vbox.Copy()];

        var dim = maxw == rw ? 'r' : maxw == gw ? 'g' : 'b';
        var lo = vbox.Lo(dim);
        var hi = vbox.Hi(dim);
        long total = 0;
        var partialsum = new long[hi - lo + 1];
        for (var i = lo; i <= hi; i++)
        {
            long sum = 0;
            switch (dim)
            {
                case 'r':
                    for (var j = vbox.G1; j <= vbox.G2; j++)
                    for (var k = vbox.B1; k <= vbox.B2; k++)
                        sum += At(histo, ColorIndex(i, j, k));
                    break;
                case 'g':
                    for (var j = vbox.R1; j <= vbox.R2; j++)
                    for (var k = vbox.B1; k <= vbox.B2; k++)
                        sum += At(histo, ColorIndex(j, i, k));
                    break;
                default:
                    for (var j = vbox.R1; j <= vbox.R2; j++)
                    for (var k = vbox.G1; k <= vbox.G2; k++)
                        sum += At(histo, ColorIndex(j, k, i));
                    break;
            }

            total += sum;
            partialsum[i - lo] = total;
        }

        long Partial(int i) => i >= lo && i <= hi ? partialsum[i - lo] : 0;
        long Lookahead(int i) => i >= lo && i <= hi ? total - partialsum[i - lo] : 0;

        for (var i = lo; i <= hi; i++)
        {
            if (Partial(i) <= total / 2.0)
                continue;

            var vbox1 = vbox.Copy();
            var vbox2 = vbox.Copy();
            var left = i - lo;
            var right = hi - i;
            var d2 = left <= right
                ? Math.Min(hi - 1, (int)(i + right / 2.0))
                : Math.Max(lo, (int)(i - 1 - left / 2.0));
            while (Partial(d2) == 0)
                d2++;
            var count2 = Lookahead(d2);
            while (count2 == 0 && Partial(d2 - 1) != 0)
            {
                d2--;
                count2 = Lookahead(d2);
            }

            vbox1.SetHi(dim, d2);
            vbox2.SetLo(dim, d2 + 1);
            return [vbox1, vbox2];
        }

        throw new InvalidOperationException("Median cut found no split");
    }

    private static void Iter(PQueue<VBox> lh, int[] histo, double target)
    {
        var ncolors = lh.Size;
        var niters = 0;
        while (niters < MaxIterations)
        {
            if (ncolors >= target)
                return;
            niters++;
            var vbox = lh.Pop();
            if (vbox.Count() == 0)
            {
                lh.Push(vbox);
                niters++;
                continue;
            }

            var vboxes = MedianCutApply(histo, vbox);
            lh.Push(vboxes[0]);
            if (vboxes.Count <= 1)
                continue;
            lh.Push(vboxes[1]);
            ncolors++;
        }
    }
}
