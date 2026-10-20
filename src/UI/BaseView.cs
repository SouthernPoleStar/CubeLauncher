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
            BackColor = Color.Transparent;
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
