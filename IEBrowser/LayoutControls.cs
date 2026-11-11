using System;
using System.Drawing;
using System.Windows.Forms;

namespace IEBrowser
{
    internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        public BufferedTableLayoutPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    /// <summary>
    /// 顶部工具栏：导航按钮 + 地址栏 + 收藏/菜单按钮。
    /// </summary>
    internal sealed class ToolbarPanel : Panel
    {
        private GlyphButton _back;
        private GlyphButton _forward;
        private GlyphButton _refresh;
        private GlyphButton _home;
        private GlyphButton _star;
        private GlyphButton _menu;
        private AddressBar _address;

        public ToolbarPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.Toolbar;
            Margin = Padding.Empty;
        }

        public void Setup(GlyphButton back, GlyphButton forward, GlyphButton refresh, GlyphButton home,
                          AddressBar address, GlyphButton star, GlyphButton menu)
        {
            _back = back;
            _forward = forward;
            _refresh = refresh;
            _home = home;
            _address = address;
            _star = star;
            _menu = menu;

            Controls.Add(_address);
            Controls.Add(_back);
            Controls.Add(_forward);
            Controls.Add(_refresh);
            Controls.Add(_home);
            Controls.Add(_star);
            Controls.Add(_menu);

            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            if (_address == null) return;

            int pad = Ui.Px(10);
            int size = Math.Min(Ui.Px(32), Height - Ui.Px(8));
            int y = (Height - size) / 2;
            int gap = Ui.Px(2);
            int x = pad;

            GlyphButton[] left = new GlyphButton[] { _back, _forward, _refresh, _home };
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] == null) continue;
                left[i].SetBounds(x, y, size, size);
                x += size + gap;
            }

            int right = Width - pad - size;
            if (_menu != null)
            {
                _menu.SetBounds(right, y, size, size);
                right -= size + gap;
            }
            if (_star != null)
            {
                _star.SetBounds(right, y, size, size);
                right -= size + gap;
            }

            int addressLeft = x + Ui.Px(6);
            int addressWidth = Math.Max(Ui.Px(60), right + size - addressLeft - Ui.Px(6));
            _address.SetBounds(addressLeft, y, addressWidth, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.Border))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }

    /// <summary>
    /// 加载进度条（细线）。
    /// </summary>
    internal sealed class ProgressLine : Control
    {
        private int _value;
        private bool _active;

        public ProgressLine()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.TabStrip;
            Height = Ui.Px(3);
        }

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                Invalidate();
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (_value == v) return;
                _value = v;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.TabStrip);
            if (!_active) return;

            int width = (int)Math.Round(Width * (_value / 100.0));
            if (width < Ui.Px(16)) width = Ui.Px(16);
            using (var brush = new SolidBrush(Ui.Accent))
            {
                g.FillRectangle(brush, 0, 0, width, Height);
            }
        }
    }

    /// <summary>
    /// 底部状态栏。
    /// </summary>
    internal sealed class SimpleStatusBar : Panel
    {
        private readonly Label _left;
        private readonly Label _right;

        public SimpleStatusBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(249, 250, 252);

            _left = new Label();
            _left.AutoSize = false;
            _left.AutoEllipsis = true;
            _left.Font = Ui.SmallFont;
            _left.ForeColor = Ui.Muted;
            _left.TextAlign = ContentAlignment.MiddleLeft;
            _left.BackColor = Color.Transparent;

            _right = new Label();
            _right.AutoSize = false;
            _right.Font = Ui.SmallFont;
            _right.ForeColor = Ui.Muted;
            _right.TextAlign = ContentAlignment.MiddleRight;
            _right.BackColor = Color.Transparent;

            Controls.Add(_left);
            Controls.Add(_right);
        }

        public string LeftText
        {
            get { return _left.Text; }
            set { _left.Text = value ?? string.Empty; }
        }

        public string RightText
        {
            get { return _right.Text; }
            set { _right.Text = value ?? string.Empty; }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = Ui.Px(10);
            int rightWidth = Ui.Px(96);
            _right.SetBounds(Width - rightWidth - pad, 0, rightWidth, Height);
            _left.SetBounds(pad, 0, Math.Max(Ui.Px(20), Width - rightWidth - pad * 2 - Ui.Px(8)), Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Ui.Border))
            {
                e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            }
        }
    }
}
