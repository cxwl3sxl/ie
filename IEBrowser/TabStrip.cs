using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace IEBrowser
{
    /// <summary>
    /// 自绘标签条：支持选中、悬停、关闭、新建。
    /// </summary>
    internal sealed class TabStrip : Control
    {
        private readonly List<string> _titles = new List<string>();
        private readonly List<Rectangle> _rects = new List<Rectangle>();
        private readonly ToolTip _tip = new ToolTip();
        private Rectangle _newTabRect;
        private int _selected = -1;
        private int _hoverIndex = -1;
        private bool _hoverClose;
        private bool _hoverNew;
        private int _tipIndex = -1;

        public event Action<int> TabSelected;
        public event Action<int> TabCloseRequested;
        public event Action NewTabRequested;

        public TabStrip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Ui.TabStrip;
            Height = Ui.Px(34);
        }

        public int Count
        {
            get { return _titles.Count; }
        }

        public int SelectedIndex
        {
            get { return _selected; }
        }

        public void AddTab(string title)
        {
            _titles.Add(title ?? "新标签页");
            if (_selected < 0) _selected = 0;
            Invalidate();
        }

        public void RemoveTab(int index)
        {
            if (index < 0 || index >= _titles.Count) return;
            _titles.RemoveAt(index);
            if (_titles.Count == 0) _selected = -1;
            else if (_selected > index) _selected--;
            else if (_selected >= _titles.Count) _selected = _titles.Count - 1;
            _hoverIndex = -1;
            Invalidate();
        }

        public void SelectTab(int index)
        {
            if (index < 0 || index >= _titles.Count || _selected == index) return;
            _selected = index;
            Invalidate();
        }

        public void SetTitle(int index, string title)
        {
            if (index < 0 || index >= _titles.Count) return;
            string text = string.IsNullOrEmpty(title) ? "新标签页" : title;
            if (_titles[index] == text) return;
            _titles[index] = text;
            if (_tipIndex == index) _tip.SetToolTip(this, text);
            Invalidate();
        }

        private int TabStep(int count)
        {
            int left = Ui.Px(8);
            int available = Width - left * 2 - Ui.Px(30) - Ui.Px(8);
            int step = count > 0 ? available / count : 0;
            if (step > Ui.Px(220)) step = Ui.Px(220);
            if (step < Ui.Px(74)) step = Ui.Px(74);
            return step;
        }

        private void BuildRects()
        {
            _rects.Clear();
            int count = _titles.Count;
            int step = TabStep(count);
            int x = Ui.Px(8);
            int top = Ui.Px(6);

            for (int i = 0; i < count; i++)
            {
                _rects.Add(new Rectangle(x, top, Math.Max(Ui.Px(48), step - Ui.Px(2)), Height - top));
                x += step;
            }

            int size = Ui.Px(26);
            _newTabRect = new Rectangle(x + Ui.Px(4), (Height - size) / 2, size, size);
        }

        private Rectangle CloseRect(Rectangle tab)
        {
            int size = Ui.Px(18);
            return new Rectangle(tab.Right - size - Ui.Px(4), tab.Y + (tab.Height - size) / 2, size, size);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Ui.TabStrip);

            BuildRects();

            using (var pen = new Pen(Ui.Border))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            for (int i = 0; i < _titles.Count && i < _rects.Count; i++)
            {
                Rectangle rect = _rects[i];
                bool isSelected = i == _selected;
                bool isHover = i == _hoverIndex;

                if (isSelected)
                {
                    var full = new Rectangle(rect.X, rect.Y, rect.Width, Height - rect.Y);
                    using (var path = Ui.RoundedTop(full, Ui.Px(7)))
                    {
                        using (var brush = new SolidBrush(Color.White))
                        {
                            g.FillPath(brush, path);
                        }
                        using (var pen = new Pen(Ui.Border))
                        {
                            g.DrawPath(pen, path);
                        }
                    }
                    using (var pen = new Pen(Color.White, 2f))
                    {
                        g.DrawLine(pen, full.X + Ui.Px(2), Height - 1, full.Right - Ui.Px(2), Height - 1);
                    }
                }
                else if (isHover)
                {
                    Ui.FillRounded(g, rect, Color.FromArgb(232, 236, 242), Ui.Px(6));
                }

                int textLeft = rect.X + Ui.Px(12);
                int textRight = rect.Right - Ui.Px(26);
                var textRect = new Rectangle(textLeft, rect.Y, Math.Max(Ui.Px(10), textRight - textLeft), rect.Height);
                Color textColor = isSelected ? Ui.Text : Ui.Muted;

                TextRenderer.DrawText(g, _titles[i], Ui.TabFont, textRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.SingleLine);

                Rectangle closeRect = CloseRect(rect);
                bool closeHover = isHover && _hoverClose;

                if (closeHover)
                {
                    Ui.FillRounded(g, closeRect, Color.FromArgb(226, 78, 78), Ui.Px(4));
                }

                Color closeColor = closeHover ? Color.White : (isSelected ? Color.FromArgb(96, 103, 112) : Color.FromArgb(146, 153, 162));
                TextRenderer.DrawText(g, "\u00D7", Ui.CloseFont, closeRect, closeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (_hoverNew)
            {
                Ui.FillRounded(g, _newTabRect, Ui.Hover, Ui.Px(6));
            }

            Color plusColor = _hoverNew ? Ui.Accent : Ui.Muted;
            int arm = Ui.Px(5);
            int cx = _newTabRect.X + _newTabRect.Width / 2;
            int cy = _newTabRect.Y + _newTabRect.Height / 2;
            using (var pen = new Pen(plusColor, 1.6f))
            {
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            BuildRects();

            int hover = -1;
            bool overClose = false;
            for (int i = 0; i < _rects.Count; i++)
            {
                if (_rects[i].Contains(e.Location))
                {
                    hover = i;
                    overClose = CloseRect(_rects[i]).Contains(e.Location);
                    break;
                }
            }

            bool overNew = _newTabRect.Contains(e.Location);

            if (hover != _hoverIndex || overClose != _hoverClose || overNew != _hoverNew)
            {
                _hoverIndex = hover;
                _hoverClose = overClose;
                _hoverNew = overNew;
                Cursor = (hover >= 0 || overNew) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }

            if (hover >= 0 && hover != _tipIndex)
            {
                _tipIndex = hover;
                _tip.SetToolTip(this, _titles[hover]);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            _hoverClose = false;
            _hoverNew = false;
            _tipIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            BuildRects();

            if (e.Button == MouseButtons.Middle)
            {
                for (int i = 0; i < _rects.Count; i++)
                {
                    if (_rects[i].Contains(e.Location))
                    {
                        RaiseClose(i);
                        return;
                    }
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (_newTabRect.Contains(e.Location))
            {
                RaiseNewTab();
                return;
            }

            for (int i = 0; i < _rects.Count; i++)
            {
                if (!_rects[i].Contains(e.Location)) continue;
                if (CloseRect(_rects[i]).Contains(e.Location)) RaiseClose(i);
                else RaiseSelect(i);
                return;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && _hoverIndex < 0 && !_newTabRect.Contains(e.Location))
            {
                RaiseNewTab();
            }
        }

        private void RaiseSelect(int index)
        {
            _selected = index;
            Invalidate();
            Action<int> handler = TabSelected;
            if (handler != null) handler(index);
        }

        private void RaiseClose(int index)
        {
            Action<int> handler = TabCloseRequested;
            if (handler != null) handler(index);
        }

        private void RaiseNewTab()
        {
            Action handler = NewTabRequested;
            if (handler != null) handler();
        }
    }
}
