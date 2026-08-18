using CabinetNC.Domain.Manufacturing;
using SkiaSharp;

namespace CabinetNC.Desktop;

/// <summary>Excitech-size shop label: 236×157 px ≈ 60×40 mm at 100 dpi.</summary>
static class LabelBmp
{
    public const int WidthPx = 236;
    public const int HeightPx = 157;

    public static byte[] Render(LabelPaste paste)
    {
        using var bmp = new SKBitmap(WidthPx, HeightPx, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        using (var border = new SKPaint
        {
            Color = new SKColor(0x22, 0x22, 0x22),
            IsStroke = true,
            StrokeWidth = 2,
            IsAntialias = true,
        })
            canvas.DrawRect(1, 1, WidthPx - 2, HeightPx - 2, border);

        var title = string.IsNullOrWhiteSpace(paste.Title) ? paste.Stem : paste.Title;
        var group = paste.Group ?? "";
        var size = $"{Fmt(paste.WidthMm)} × {Fmt(paste.HeightMm)} mm";
        var stock = string.IsNullOrWhiteSpace(paste.Material)
            ? (paste.ThicknessMm > 0 ? $"{Fmt(paste.ThicknessMm)} mm" : "")
            : paste.Material;
        var sheet = $"S{paste.SheetIndex + 1}";

        DrawLine(canvas, title, 10, 38, 22, bold: true);
        if (!string.IsNullOrWhiteSpace(group) &&
            !group.Equals(title, StringComparison.OrdinalIgnoreCase))
            DrawLine(canvas, group, 10, 62, 13, bold: false);
        DrawLine(canvas, size, 10, 88, 14, bold: false);
        if (!string.IsNullOrWhiteSpace(stock))
            DrawLine(canvas, stock, 10, 110, 12, bold: false);
        DrawLine(canvas, $"{sheet}  {paste.Stem}", 10, 140, 11, bold: false,
            color: new SKColor(0x55, 0x55, 0x55));

        return ToBmp24(bmp);
    }

    static void DrawLine(
        SKCanvas canvas, string text, float x, float y, float size, bool bold,
        SKColor? color = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        using var paint = new SKPaint { Color = color ?? new SKColor(0x11, 0x11, 0x11), IsAntialias = true };
        using var font = new SKFont(UiTypeface(bold), size);
        var max = WidthPx - x - 8;
        var shown = Ellipsize(text, max, font);
        canvas.DrawText(shown, x, y, SKTextAlign.Left, font, paint);
    }

    static string Ellipsize(string text, float maxWidth, SKFont font)
    {
        if (font.MeasureText(text) <= maxWidth) return text;
        const string ell = "…";
        for (var n = text.Length - 1; n >= 1; n--)
        {
            var cut = text[..n] + ell;
            if (font.MeasureText(cut) <= maxWidth) return cut;
        }
        return ell;
    }

    static string Fmt(double v) =>
        v.ToString(v >= 100 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);

    static SKTypeface? _regular;
    static SKTypeface? _bold;

    static SKTypeface UiTypeface(bool bold)
    {
        if (bold)
            return _bold ??= Resolve(true);
        return _regular ??= Resolve(false);
    }

    static SKTypeface Resolve(bool bold)
    {
        var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
        foreach (var family in new[]
                 {
                     "Microsoft YaHei UI",
                     "Microsoft YaHei",
                     "微软雅黑",
                     "Noto Sans CJK SC",
                     "Segoe UI",
                 })
        {
            var tf = SKTypeface.FromFamilyName(family, style);
            if (tf is null) continue;
            if (tf.ContainsGlyph('板') || family is "Segoe UI")
                return tf;
            tf.Dispose();
        }
        return SKTypeface.Default;
    }

    static byte[] ToBmp24(SKBitmap bmp)
    {
        var w = bmp.Width;
        var h = bmp.Height;
        var stride = (w * 3 + 3) & ~3;
        var pixels = stride * h;
        var file = 14 + 40 + pixels;
        var bytes = new byte[file];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.TryWriteBytes(bytes.AsSpan(2, 4), file);
        BitConverter.TryWriteBytes(bytes.AsSpan(10, 4), 54);
        BitConverter.TryWriteBytes(bytes.AsSpan(14, 4), 40);
        BitConverter.TryWriteBytes(bytes.AsSpan(18, 4), w);
        BitConverter.TryWriteBytes(bytes.AsSpan(22, 4), h);
        BitConverter.TryWriteBytes(bytes.AsSpan(26, 2), (short)1);
        BitConverter.TryWriteBytes(bytes.AsSpan(28, 2), (short)24);

        var src = bmp.Pixels;
        for (var y = 0; y < h; y++)
        {
            var destRow = 54 + (h - 1 - y) * stride;
            var srcRow = y * w;
            for (var x = 0; x < w; x++)
            {
                var c = src[srcRow + x];
                var o = destRow + x * 3;
                bytes[o] = c.Blue;
                bytes[o + 1] = c.Green;
                bytes[o + 2] = c.Red;
            }
        }
        return bytes;
    }
}
