using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// 圆角地址栏（内部承载无边框文本框）。
    /// </summary>
    internal sealed class AddressBar : Control
    {
        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TextBox _box;
        private bool _hover;
        private bool _focused;

        public event Action<string> NavigateRequested;
        public event Action CancelRequested;

        public AddressBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Height = Ui.Px(32);

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.Font = Ui.BaseFont;
            _box.ForeColor = Ui.Text;
            _box.BackColor = Color.White;
            _box.KeyDown += BoxKeyDown;
            _box.GotFocus += delegate { _focused = true; Invalidate(); };
            _box.LostFocus += delegate { _focused = false; Invalidate(); };
            _box.HandleCreated += delegate
            {
                try { SendMessage(_box.Handle, EmSetCueBanner, (IntPtr)1, "搜索或输入网址"); }
                catch { }
            };
            Controls.Add(_box);
        }

        public string AddressText
        {
            get { return _box.Text; }
            set
            {
                string text = value ?? string.Empty;
                if (_box.Text == text) return;
                _box.Text = text;
                _box.SelectionStart = _box.TextLength;
            }
        }

        public override string Text
        {
            get { return _box.Text; }
            set { AddressText = value; }
        }

        public void FocusAndSelect()
        {
            Focus();
            _box.Focus();
            _box.SelectAll();
        }

        public bool ContainsAddressFocus
        {
            get { return _box.Focused; }
        }

        public void AddAutoComplete(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                if (!_box.AutoCompleteCustomSource.Contains(url))
                {
                    _box.AutoCompleteCustomSource.Add(url);
                }
            }
            catch { }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _box.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _box.AutoCompleteSource = AutoCompleteSource.CustomSource;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_box == null) return;
            int left = Ui.Px(30);
            int height = _box.PreferredHeight;
            _box.SetBounds(left, (Height - height) / 2, Math.Max(Ui.Px(20), Width - left - Ui.Px(10)), height);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            FocusAndSelect();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Ui.Toolbar);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.Rounded(rect, Ui.Px(8)))
            {
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillPath(brush, path);
                }

                Color border = _focused
                    ? Ui.Accent
                    : (_hover ? Color.FromArgb(178, 185, 194) : Ui.Border);
                using (var pen = new Pen(border, _focused ? 1.6f : 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            DrawGlobe(g);
        }

        private void DrawGlobe(Graphics g)
        {
            float cx = Ui.Px(16);
            float cy = Height / 2f;
            float r = Ui.Px(6);

            Color color = _focused ? Ui.Accent : Color.FromArgb(130, 138, 148);
            using (var pen = new Pen(color, 1.2f))
            {
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                g.DrawEllipse(pen, cx - r / 2f, cy - r, r, r * 2);
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
            }
        }

        private void BoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Action<string> handler = NavigateRequested;
                if (handler != null) handler(_box.Text);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                Action handler = CancelRequested;
                if (handler != null) handler();
            }
        }
    }
}
