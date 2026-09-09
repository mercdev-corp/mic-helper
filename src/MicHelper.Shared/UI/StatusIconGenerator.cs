using System.Drawing;
using System.Drawing.Drawing2D;
using MicHelper.Shared.Protocol;

namespace MicHelper.Shared.UI;

public static class StatusIconGenerator
{
    private static byte[]? s_micMutedData;
    private static byte[]? s_micUnmutedData;
    private static byte[]? s_deviceDisconnectedData;
    private static byte[]? s_serverDisconnectedData;
    private static byte[]? s_pauseData;
    private static byte[]? s_appIconData;

    public static Bitmap? GetMicMutedBitmap()
    {
        s_micMutedData ??= LoadAssetBytes("mic-muted-2048.png", "mic-muted-256.png", "mic-muted.png");
        return CreateBitmapFromBytes(s_micMutedData);
    }

    public static Bitmap? GetMicUnmutedBitmap()
    {
        s_micUnmutedData ??= LoadAssetBytes("mic-unmuted-2048.png", "mic-unmuted-256.png", "mic-unmuted.png");
        return CreateBitmapFromBytes(s_micUnmutedData);
    }

    public static Bitmap? GetDeviceDisconnectedBitmap()
    {
        s_deviceDisconnectedData ??= LoadAssetBytes("device-disconnected-2048.png", "device-disconnected-256.png", "device-disconnected.png");
        return CreateBitmapFromBytes(s_deviceDisconnectedData);
    }

    public static Bitmap? GetServerDisconnectedBitmap()
    {
        s_serverDisconnectedData ??= LoadAssetBytes("server-disconnected-2048.png", "server-disconnected-256.png", "server-disconnected.png");
        return CreateBitmapFromBytes(s_serverDisconnectedData);
    }

    public static Bitmap? GetPauseBitmap()
    {
        s_pauseData ??= LoadAssetBytes("pause-2048.png", "pause-256.png", "pause.png");
        return CreateBitmapFromBytes(s_pauseData);
    }

    private static Bitmap? CreateBitmapFromBytes(byte[]? data)
    {
        if (data != null && data.Length > 0)
        {
            try
            {
                using var ms = new MemoryStream(data, writable: false);
                using var temp = new Bitmap(ms);
                return new Bitmap(temp);
            }
            catch
            {
            }
        }

        return null;
    }

    public static Icon GetAppIcon()
    {
        if (s_appIconData == null)
        {
            s_appIconData = LoadAssetBytes("app.ico");
        }

        if (s_appIconData != null && s_appIconData.Length > 0)
        {
            try
            {
                using var ms = new MemoryStream(s_appIconData, writable: false);
                using var temp = new Icon(ms);
                return (Icon)temp.Clone();
            }
            catch
            {
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static byte[]? LoadAssetBytes(params string[] filenames)
    {
        var assembly = typeof(StatusIconGenerator).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var filename in filenames)
        {
            var resName = resourceNames.FirstOrDefault(n => n.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            if (resName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var filename in filenames)
        {
            var diskPaths = new[]
            {
                Path.Combine(baseDir, "assets", filename),
                Path.Combine(baseDir, "..", "..", "..", "assets", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "assets", filename)
            };

            foreach (var p in diskPaths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        return File.ReadAllBytes(p);
                    }
                    catch
                    {
                    }
                }
            }
        }

        return null;
    }

    public static Icon CreateServerStatusIcon(MicState state) => CreateStatusIcon(state, isServer: true);
    public static Icon CreateClientStatusIcon(MicState state) => CreateStatusIcon(state, isServer: false);

    public static Icon CreateStatusIcon(MicState state) => CreateStatusIcon(state, isServer: false);

    public static Icon CreateStatusIcon(MicState state, bool isServer)
    {
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);

        switch (state)
        {
            case MicState.Muted:
                using (var mutedBmp = GetMicMutedBitmap())
                {
                    if (mutedBmp != null && mutedBmp.Width > 0 && mutedBmp.Height > 0)
                    {
                        DrawImagePreservingAspect(g, mutedBmp, new Rectangle(0, 0, 32, 32));
                    }
                    else
                    {
                        DrawMicrophone(g, Color.FromArgb(220, 53, 69)); // Bootstrap danger red
                        using var slashPen = new Pen(Color.FromArgb(255, 60, 60), 3f);
                        g.DrawLine(slashPen, 6, 6, 26, 26);
                    }
                }
                break;

            case MicState.Unmuted:
                using (var unmutedBmp = GetMicUnmutedBitmap())
                {
                    if (unmutedBmp != null && unmutedBmp.Width > 0 && unmutedBmp.Height > 0)
                    {
                        DrawImagePreservingAspect(g, unmutedBmp, new Rectangle(0, 0, 32, 32));
                    }
                    else
                    {
                        // Green active mic
                        DrawMicrophone(g, Color.FromArgb(40, 167, 69)); // Bootstrap success green
                    }
                }
                break;

            case MicState.Disconnected:
                using (var discBmp = isServer
                           ? (GetDeviceDisconnectedBitmap() ?? GetServerDisconnectedBitmap())
                           : GetServerDisconnectedBitmap())
                {
                    if (discBmp != null && discBmp.Width > 0 && discBmp.Height > 0)
                    {
                        DrawImagePreservingAspect(g, discBmp, new Rectangle(0, 0, 32, 32));
                    }
                    else
                    {
                        // Gray/Amber warning
                        DrawMicrophone(g, Color.FromArgb(108, 117, 125)); // Gray
                        using var warnBrush = new SolidBrush(Color.FromArgb(255, 193, 7)); // Amber
                        g.FillEllipse(warnBrush, 18, 18, 12, 12);
                        using var markBrush = new SolidBrush(Color.Black);
                        using var font = new Font("Arial", 8f, FontStyle.Bold);
                        g.DrawString("!", font, markBrush, 21, 17);
                    }
                }
                break;

            case MicState.Paused:
                using (var pauseBmp = GetPauseBitmap())
                {
                    if (pauseBmp != null && pauseBmp.Width > 0 && pauseBmp.Height > 0)
                    {
                        DrawImagePreservingAspect(g, pauseBmp, new Rectangle(0, 0, 32, 32));
                    }
                    else
                    {
                        // Amber/Gray paused bars
                        DrawMicrophone(g, Color.FromArgb(108, 117, 125));
                        using var pauseBrush = new SolidBrush(Color.FromArgb(255, 193, 7));
                        g.FillRectangle(pauseBrush, 19, 18, 4, 11);
                        g.FillRectangle(pauseBrush, 26, 18, 4, 11);
                    }
                }
                break;
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            // Clone so we can safely destroy the native handle
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static void DrawImagePreservingAspect(Graphics g, Image img, Rectangle destRect)
    {
        float scale = Math.Min((float)destRect.Width / img.Width, (float)destRect.Height / img.Height);
        int targetW = Math.Max(1, (int)Math.Round(img.Width * scale));
        int targetH = Math.Max(1, (int)Math.Round(img.Height * scale));
        int targetX = destRect.X + (destRect.Width - targetW) / 2;
        int targetY = destRect.Y + (destRect.Height - targetH) / 2;

        using var wrapMode = new System.Drawing.Imaging.ImageAttributes();
        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(img, new Rectangle(targetX, targetY, targetW, targetH), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, wrapMode);
    }

    private static void DrawMicrophone(Graphics g, Color color)
    {
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 2f);

        // Capsule body
        g.FillRoundedRectangle(brush, 10, 4, 12, 16, 6);

        // Arc support
        g.DrawArc(pen, 7, 9, 18, 13, 0, 180);

        // Base stand
        g.DrawLine(pen, 16, 22, 16, 28);
        g.DrawLine(pen, 11, 28, 21, 28);
    }

    private static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
