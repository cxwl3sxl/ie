using System;
using System.Drawing;
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
    /// </summary>
    internal sealed class GlyphButton : Control
    {
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
