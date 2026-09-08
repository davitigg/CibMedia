using Android.Graphics;
using QRCoder;

namespace CibMedia.AndroidTv.Platform;

// QRCoder's PNG encoder rather than its image ones, which pull in System.Drawing or ImageSharp.
public static class QrCode
{
    public static Bitmap? Render(string content, int sizePx, Color modules, Color background)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);

        var png = new PngByteQRCode(data).GetGraphic(
            ModulesToPixels(data, sizePx),
            Rgba(modules),
            Rgba(background));

        return BitmapFactory.DecodeByteArray(png, 0, png.Length);
    }

    // GetGraphic takes pixels per module, not an overall size. Never below 1, or the encoder
    // produces nothing.
    private static int ModulesToPixels(QRCodeData data, int sizePx)
    {
        var modules = data.ModuleMatrix.Count;

        return modules <= 0 ? 1 : Math.Max(1, sizePx / modules);
    }

    private static byte[] Rgba(Color color)
    {
        return [color.R, color.G, color.B, color.A];
    }
}
