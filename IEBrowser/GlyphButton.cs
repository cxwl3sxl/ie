using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace IEBrowser
{
    internal enum GlyphIcon
    {
        Back,
        Forward,
        Refresh,
        Stop,
        Home,
        Star,
        StarFilled,
        Menu
    }

    /// <summary>
    /// 轻量扁平图标按钮。
    /// 图标来源：优先加载 exe 旁 images\ 目录下的 PNG 图像（按图标名映射），
    /// 未找到或加载失败时回退到内置字体字形绘制。
    /// </summary>
    internal sealed class GlyphButton : Control
    {
        // PNG 文件名（不含扩展名），按 GlyphIcon 枚举映射
        private static readonly Dictionary<GlyphIcon, string> IconFileNames = new Dictionary<GlyphIcon, string>
        {
            { GlyphIcon.Back, "后退" },
            { GlyphIcon.Forward, "前进" },
            { GlyphIcon.Refresh, "刷新" },
            { GlyphIcon.Stop, "刷新" },          // 加载中暂用刷新图；后续可提供独立的"停止"图
            { GlyphIcon.Home, "主页" },
            { GlyphIcon.Star, "收藏" },
            { GlyphIcon.StarFilled, "已收藏" },
            { GlyphIcon.Menu, "菜单" }
        };

        // GlyphIcon → 禁用态 PNG 文件名（仅 Back/Forward 提供）
        private static readonly Dictionary<GlyphIcon, string> DisabledFileNames = new Dictionary<GlyphIcon, string>
        {
            { GlyphIcon.Back, "后退-禁用" },
            { GlyphIcon.Forward, "前进-禁用" }
        };

        private static readonly Dictionary<string, Image> _imageCache = new Dictionary<string, Image>();
        private static bool _imageDirProbed;
        private static string _imageDir;

        private GlyphIcon _icon;
        private bool _hover;
        private bool _pressed;

        public GlyphButton(GlyphIcon icon)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.Transparent;
            _icon = icon;
            TabStop = false;
            Size = new Size(Ui.Px(32), Ui.Px(32));
        }

        public GlyphIcon Icon
        {
            get { return _icon; }
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// 从程序集内嵌资源加载 PNG 图像（资源名 IEBrowser.img.&lt;文件名&gt;）。
        /// </summary>
        private static Image LoadIconImage(GlyphIcon icon, bool disabled)
        {
            string name;
            if (disabled && DisabledFileNames.TryGetValue(icon, out name)) { }
            else if (!IconFileNames.TryGetValue(icon, out name)) return null;

            string resource = "IEBrowser.img." + name;
            Image img;
            lock (_imageCache)
            {
                if (_imageCache.TryGetValue(resource, out img)) return img;
            }
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
                if (stream != null)
                {
                    img = new Bitmap(stream);
                    stream.Dispose();
                    lock (_imageCache) _imageCache[resource] = img;
                    return img;
                }
            }
            catch { }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Ui.Toolbar);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_pressed)
            {
                Ui.FillRounded(g, r, Ui.Pressed, Ui.Px(7));
            }
            else if (_hover)
            {
                Ui.FillRounded(g, r, Ui.Hover, Ui.Px(7));
            }

            Image img = LoadIconImage(_icon, !Enabled);
            if (img != null)
            {
                // 等比缩放居中绘制，占按钮约 60%
                int target = Math.Min(Width, Height) * 3 / 5;
                int w = img.Width >= img.Height ? target : target * img.Width / img.Height;
                int h = img.Width >= img.Height ? target * img.Height / img.Width : target;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, (Width - w) / 2, (Height - h) / 2, w, h);
                return;
            }

            // 回退：字体字形绘制
            Color color = Enabled ? Ui.IconColor : Ui.IconDisabled;
            string glyph;
            Font font;

            switch (_icon)
            {
                case GlyphIcon.Back: glyph = "\u2190"; font = Ui.IconFont; break;
                case GlyphIcon.Forward: glyph = "\u2192"; font = Ui.IconFont; break;
                case GlyphIcon.Refresh: glyph = "\u21BB"; font = Ui.IconFont; break;
                case GlyphIcon.Stop: glyph = "\u2715"; font = Ui.IconFont; break;
                case GlyphIcon.Home: glyph = "\u2302"; font = Ui.IconFont; break;
                case GlyphIcon.Star: glyph = "\u2606"; font = Ui.IconFont; break;
                case GlyphIcon.StarFilled: glyph = "\u2605"; font = Ui.IconFont; break;
                default: glyph = "\u22EF"; font = Ui.MenuFont; break;
            }

            if (_icon == GlyphIcon.StarFilled)
            {
                color = Ui.StarColor;
            }

            using (var brush = new SolidBrush(color))
            {
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.DrawString(glyph, font, brush, new RectangleF(0, -Ui.Px(1), Width, Height), sf);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Cursor = Cursors.Hand;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }
    }
}
