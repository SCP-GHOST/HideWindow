using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace HideWindow.Services;

public class BackgroundSkin
{
    public string? ImagePath { get; private set; }
    public int DarkenPercent { get; private set; } = 65;
    public int BlurRadius { get; private set; } = 0;

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    public static string GetSkinDirectory()
    {
        string skinDir = Path.Combine(Path.GetTempPath(), "HideWindow", "skin");
        EnsureExtracted(skinDir);
        return skinDir;
    }

    private static void EnsureExtracted(string skinDir)
    {
        Directory.CreateDirectory(skinDir);

        Assembly asm = Assembly.GetExecutingAssembly();
        string prefix = "HideWindow.Skin.";

        foreach (string? resourceName in asm.GetManifestResourceNames())
        {
            if (resourceName is null || !resourceName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string fileName = resourceName[prefix.Length..];
            string targetPath = Path.Combine(skinDir, fileName);

            using Stream src = asm.GetManifestResourceStream(resourceName)!;
            using FileStream dst = File.Create(targetPath);
            src.CopyTo(dst);
        }
    }

    public static BackgroundSkin Load(string skinDir)
    {
        var skin = new BackgroundSkin();

        string? bgImage = null;
        foreach (string ext in ImageExtensions)
        {
            string[] files = Directory.GetFiles(skinDir, "*" + ext, SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                bgImage = files[0];
                break;
            }
        }

        if (bgImage != null)
            skin.ImagePath = bgImage;

        string cfgPath = Path.Combine(skinDir, "background.cfg");
        if (File.Exists(cfgPath))
        {
            foreach (string line in File.ReadAllLines(cfgPath))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
                    continue;

                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;

                string key = trimmed[..eq].Trim().ToLowerInvariant();
                string val = trimmed[(eq + 1)..].Trim();

                switch (key)
                {
                    case "darken":
                        if (int.TryParse(val, out int d))
                            skin.DarkenPercent = Math.Clamp(d, 0, 100);
                        break;
                    case "blur":
                        if (int.TryParse(val, out int b))
                            skin.BlurRadius = Math.Clamp(b, 0, 50);
                        break;
                }
            }
        }

        return skin;
    }

    public void Apply(Window window, System.Windows.Controls.Image bgImage,
                      System.Windows.Shapes.Rectangle darkenOverlay)
    {
        if (bgImage == null || darkenOverlay == null) return;

        if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(ImagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                bgImage.Source = bitmap;
                bgImage.Effect = BlurRadius > 0
                    ? new BlurEffect { Radius = BlurRadius }
                    : null;
            }
            catch
            {
                bgImage.Source = null;
            }
        }
        else
        {
            bgImage.Source = null;
        }

        byte alpha = (byte)(DarkenPercent * 255 / 100);
        // 暖黑色调(参考 Deadlock Mod Manager 的 --background: 20 9% 6%)
        darkenOverlay.Fill = new SolidColorBrush(Color.FromArgb(alpha, 17, 15, 14));
    }
}
