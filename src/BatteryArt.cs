using System;
using System.Drawing;
using System.Drawing.Drawing2D;

internal static class BatteryArt
{
    internal static Color LevelColor(int level)
    {
        return level < 0 ? Color.FromArgb(148, 163, 184) :
            level <= 20 ? Color.FromArgb(239, 68, 68) :
            level <= 50 ? Color.FromArgb(249, 158, 36) : Color.FromArgb(46, 211, 116);
    }

    internal static GraphicsPath Rounded(float x, float y, float w, float h, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Bitmap Tray(int level, int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 64f, size / 64f);
            float width = Math.Max(0, Math.Min(100, level)) * .50f;
            using (var body = Rounded(1, 15, 55, 34, 9))
            using (var inner = Rounded(4, 18, 49, 28, 6))
            using (var shell = new SolidBrush(Color.FromArgb(190, 197, 205)))
            using (var baseFill = new SolidBrush(Color.FromArgb(47, 52, 59)))
            using (var fill = new SolidBrush(LevelColor(level)))
            {
                g.FillPath(shell, body);
                g.FillEllipse(shell, 57, 26, 6, 12);
                g.FillPath(baseFill, inner);
                g.SetClip(inner);
                g.FillRectangle(fill, 4, 18, width, 28);
                g.ResetClip();
                string label = level < 0 ? "?" : level.ToString();
                using (var font = new Font("Segoe UI", level == 100 ? 23 : 25, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var box = new RectangleF(1, 13, 55, 36);
                    g.DrawString(label, font, Brushes.White, box, format);
                    g.SetClip(new RectangleF(4, 18, width, 28));
                    g.DrawString(label, font, Brushes.Black, box, format);
                    g.ResetClip();
                }
            }
        }
        return bitmap;
    }

    internal static Bitmap Logo(int size)
    {
        var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 256f, size / 256f);
            using (var tile = Rounded(8, 8, 240, 240, 54))
            using (var ink = new LinearGradientBrush(new Point(28, 12), new Point(220, 244), Color.FromArgb(58, 69, 83), Color.FromArgb(18, 24, 34)))
                g.FillPath(ink, tile);
            g.TranslateTransform(128, 127);
            g.RotateTransform(-9);
            using (var rim = Rounded(-91, -60, 182, 129, 14))
            using (var glass = Rounded(-91, -64, 182, 127, 14))
            using (var metal = new SolidBrush(Color.FromArgb(148, 158, 171)))
            using (var pearl = new LinearGradientBrush(new Point(-70,-65), new Point(64,64), Color.White, Color.FromArgb(207,215,225)))
            using (var border = new Pen(Color.White, 2))
            {
                g.FillPath(metal, rim);
                g.FillPath(pearl, glass);
                g.DrawPath(border, glass);
            }
        }
        return bitmap;
    }
}
