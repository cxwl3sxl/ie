using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// 全局视觉与 DPI 辅助。
    /// </summary>
    internal static class Ui
    {
        public static readonly float DpiScale;

        public static readonly Font BaseFont;
        public static readonly Font SmallFont;
        public static readonly Font TabFont;
        public static readonly Font CloseFont;
        public static readonly Font IconFont;
        public static readonly Font MenuFont;

        public static readonly Color Accent = Color.FromArgb(15, 108, 189);
        public static readonly Color AccentLight = Color.FromArgb(231, 241, 250);
        public static readonly Color Toolbar = Color.White;
        public static readonly Color TabStrip = Color.FromArgb(243, 245, 248);
        public static readonly Color Border = Color.FromArgb(222, 226, 232);
        public static readonly Color Text = Color.FromArgb(31, 35, 40);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color IconColor = Color.FromArgb(66, 74, 84);
        public static readonly Color IconDisabled = Color.FromArgb(192, 197, 204);
        public static readonly Color Hover = Color.FromArgb(234, 238, 243);
        public static readonly Color Pressed = Color.FromArgb(219, 225, 233);
        public static readonly Color StarColor = Color.FromArgb(240, 160, 30);

        static Ui()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    DpiScale = g.DpiX / 96f;
                }
            }
            catch
            {
                DpiScale = 1f;
            }

            if (DpiScale < 0.5f || DpiScale > 4f)
            {
                DpiScale = 1f;
            }

            BaseFont = CreateFont(9f);
            SmallFont = CreateFont(8.25f);
            TabFont = CreateFont(9f);
            CloseFont = CreateFont(10f);
            IconFont = CreateSymbolFont(12.5f);
            MenuFont = CreateSymbolFont(13f);
        }

        private static Font CreateFont(float size)
        {
            try { return new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Point); }
        }

        private static Font CreateSymbolFont(float size)
        {
            try { return new Font("Segoe UI Symbol", size, FontStyle.Regular, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size, FontStyle.Regular, GraphicsUnit.Point); }
        }

        public static int Px(int value)
        {
            return (int)Math.Round(value * DpiScale);
        }

        public static void FillRounded(Graphics g, Rectangle rect, Color color, int radius)
        {
            using (var path = Rounded(rect, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// 顶部圆角、底部开口（用于标签页）。
        /// </summary>
        public static GraphicsPath RoundedTop(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Y + d / 2, rect.Right, rect.Bottom);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.AddLine(rect.X, rect.Bottom, rect.X, rect.Y + d / 2);
            path.CloseFigure();
            return path;
        }
    }
}
