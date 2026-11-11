using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace IEBrowser
{
    internal sealed class FavoriteItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// 收藏夹：保存在 %AppData%\IEBrowser\favorites.xml。
    /// </summary>
    internal sealed class FavoritesStore
    {
        private readonly List<FavoriteItem> _items = new List<FavoriteItem>();

        private static string FolderPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IEBrowser");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(FolderPath, "favorites.xml"); }
        }

        public IList<FavoriteItem> Items
        {
            get { return _items; }
        }

        public bool Contains(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            foreach (FavoriteItem item in _items)
            {
                if (string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public void Add(string title, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (Contains(url)) return;
            _items.Add(new FavoriteItem
            {
                Title = string.IsNullOrEmpty(title) ? url : title,
                Url = url
            });
        }

        public void Remove(string url)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_items[i].Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    _items.RemoveAt(i);
                }
            }
        }

        public void Load()
        {
            _items.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                XDocument doc = XDocument.Load(FilePath);
                XElement root = doc.Root;
                if (root == null) return;

                foreach (XElement element in root.Elements("item"))
                {
                    string title = (string)element.Attribute("title");
                    string url = (string)element.Attribute("url");
                    if (string.IsNullOrEmpty(url)) continue;
                    _items.Add(new FavoriteItem { Title = title ?? url, Url = url });
                }
            }
            catch
            {
                _items.Clear();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                var root = new XElement("favorites");
                foreach (FavoriteItem item in _items)
                {
                    root.Add(new XElement("item",
                        new XAttribute("title", item.Title ?? string.Empty),
                        new XAttribute("url", item.Url ?? string.Empty)));
                }
                new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(FilePath);
            }
            catch
            {
            }
        }
    }
}
