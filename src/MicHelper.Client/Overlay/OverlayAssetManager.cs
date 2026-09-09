using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MicHelper.Shared.Protocol;
using MicHelper.Shared.UI;

namespace MicHelper.Client.Overlay;

public sealed class OverlayAssetManager : IDisposable
{
    private readonly string _cacheDirectory;
    private Bitmap? _masterMuted;
    private Bitmap? _masterDisconnected;

    private Bitmap? _cachedMuted;
    private Bitmap? _cachedDisconnected;
    private Size _cachedSize;

    public Size CurrentSize => _cachedSize;

    public OverlayAssetManager(string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _cacheDirectory = Path.Combine(baseDir, "cache");

        LoadMasterAssets(baseDir);
    }

    private void LoadMasterAssets(string baseDir)
    {
        // 1. Mic Muted Master Asset
        var mutedFilenames = new[] { "mic-muted-2048.png", "mic-muted-256.png", "mic-muted.png" };
        foreach (var filename in mutedFilenames)
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
                        using var stream = File.OpenRead(p);
                        using var temp = new Bitmap(stream);
                        _masterMuted = new Bitmap(temp);
                        break;
                    }
                    catch
                    {
                    }
                }
            }

            if (_masterMuted != null) break;
        }

        if (_masterMuted == null)
        {
            try
            {
                using var embedded = StatusIconGenerator.GetMicMutedBitmap();
                if (embedded != null && embedded.Width > 0)
                {
                    _masterMuted = new Bitmap(embedded);
                }
            }
            catch
            {
            }
        }

        if (_masterMuted == null)
        {
            _masterMuted = GenerateDefaultMasterMuted(256);
        }

        // 2. Server Disconnected Master Asset
        var discFilenames = new[] { "server-disconnected-2048.png", "server-disconnected-256.png", "server-disconnected.png" };
        foreach (var filename in discFilenames)
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
                        using var stream = File.OpenRead(p);
                        using var temp = new Bitmap(stream);
                        _masterDisconnected = new Bitmap(temp);
                        break;
                    }
                    catch
                    {
                    }
                }
            }

            if (_masterDisconnected != null) break;
        }

        if (_masterDisconnected == null)
        {
            try
            {
                using var embedded = StatusIconGenerator.GetServerDisconnectedBitmap();
                if (embedded != null && embedded.Width > 0)
                {
                    _masterDisconnected = new Bitmap(embedded);
                }
            }
            catch
            {
            }
        }

        if (_masterDisconnected == null)
        {
            _masterDisconnected = GenerateDefaultMasterDisconnected(256);
        }
    }

    public void PreRenderAndCache(int width, int height)
    {
        width = Math.Max(32, Math.Min(width, 1024));
        height = Math.Max(32, Math.Min(height, 1024));

        _cachedSize = new Size(width, height);

        // Pre-render scaled muted bitmap
        _cachedMuted?.Dispose();
        _cachedMuted = ScaleBitmapHighQuality(_masterMuted!, width, height);

        // Pre-render scaled disconnected bitmap
        _cachedDisconnected?.Dispose();
        _cachedDisconnected = ScaleBitmapHighQuality(_masterDisconnected!, width, height);

        // Save to disk cache
        try
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            // Clean up any legacy dimension-suffixed cache files (e.g. *-*x*.png)
            foreach (var legacyFile in Directory.EnumerateFiles(_cacheDirectory, "*-*x*.png"))
            {
                try { File.Delete(legacyFile); } catch { }
            }

            var mutedCachePath = Path.Combine(_cacheDirectory, "mic-muted_resized.png");
            var discCachePath = Path.Combine(_cacheDirectory, "server-disconnected_resized.png");

            SaveBitmapToFile(_cachedMuted, mutedCachePath);
            SaveBitmapToFile(_cachedDisconnected, discCachePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write asset cache: {ex.Message}");
        }
    }

    private static void SaveBitmapToFile(Bitmap bmp, string path)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        File.WriteAllBytes(path, ms.ToArray());
    }

    public Bitmap? GetBitmap(MicState state)
    {
        return state switch
        {
            MicState.Muted => _cachedMuted,
            MicState.Disconnected => _cachedDisconnected,
            _ => null
        };
    }

    public static Bitmap ScaleBitmapHighQuality(Bitmap source, int width, int height)
    {
        var dest = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(dest);

        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float scale = Math.Min((float)width / source.Width, (float)height / source.Height);
        int targetW = Math.Max(1, (int)Math.Round(source.Width * scale));
        int targetH = Math.Max(1, (int)Math.Round(source.Height * scale));
        int targetX = (width - targetW) / 2;
        int targetY = (height - targetH) / 2;

        using var wrapMode = new ImageAttributes();
        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(source, new Rectangle(targetX, targetY, targetW, targetH), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, wrapMode);

        return dest;
    }

    private static Bitmap GenerateDefaultMasterMuted(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Outer circular glow
        using var glowBrush = new SolidBrush(Color.FromArgb(180, 220, 53, 69));
        g.FillEllipse(glowBrush, size * 0.05f, size * 0.05f, size * 0.9f, size * 0.9f);

        // Microphone silhouette
        using var whiteBrush = new SolidBrush(Color.White);
        using var pen = new Pen(Color.White, size * 0.06f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        float cx = size * 0.5f;
        float cy = size * 0.42f;
        float w = size * 0.28f;
        float h = size * 0.42f;

        // Capsule
        g.FillRoundedRectangle(whiteBrush, cx - w / 2, cy - h / 2, w, h, w / 2);

        // Arc
        g.DrawArc(pen, cx - w * 0.8f, cy - h * 0.3f, w * 1.6f, h * 0.9f, 0, 180);

        // Stand & base
        g.DrawLine(pen, cx, cy + h * 0.6f, cx, cy + h * 0.9f);
        g.DrawLine(pen, cx - w * 0.6f, cy + h * 0.9f, cx + w * 0.6f, cy + h * 0.9f);

        // Diagonal strikeout
        using var slashPen = new Pen(Color.FromArgb(255, 40, 40), size * 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(slashPen, size * 0.18f, size * 0.18f, size * 0.82f, size * 0.82f);

        return bmp;
    }

    private static Bitmap GenerateDefaultMasterDisconnected(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Outer circular warning glow
        using var glowBrush = new SolidBrush(Color.FromArgb(200, 108, 117, 125));
        g.FillEllipse(glowBrush, size * 0.05f, size * 0.05f, size * 0.9f, size * 0.9f);

        // Exclamation / warning mark
        using var yellowBrush = new SolidBrush(Color.FromArgb(255, 193, 7));
        g.FillEllipse(yellowBrush, size * 0.15f, size * 0.15f, size * 0.7f, size * 0.7f);

        using var markBrush = new SolidBrush(Color.FromArgb(33, 37, 41));
        using var font = new Font("Arial", size * 0.45f, FontStyle.Bold);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("!", font, markBrush, new RectangleF(0, size * 0.05f, size, size), sf);

        return bmp;
    }

    public void Dispose()
    {
        _cachedMuted?.Dispose();
        _cachedDisconnected?.Dispose();
        _masterMuted?.Dispose();
        _masterDisconnected?.Dispose();
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
