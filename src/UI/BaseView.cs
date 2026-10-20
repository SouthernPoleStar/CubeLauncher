using System;
using System.Drawing;
using System.Windows.Forms;

namespace CubeLauncher.UI
{
    public abstract class BaseView : BufferedUserControl
    {
        protected readonly MainForm Main;

        protected BaseView(MainForm main)
        {
            Main = main;
            // Opaque page background: while the next page slides in, the old
            // page remains behind it and no white/black gap is exposed.
            BackColor = Theme.Bg;
        }

        public virtual void OnShown()
        {
            RefreshView();
        }

        public virtual void RefreshView()
        {
        }
    }
}
