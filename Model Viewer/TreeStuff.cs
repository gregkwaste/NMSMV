using System;
using System.Windows.Forms;

namespace Model_Viewer
{
    class MyTreeNode: TreeNode
    {
        //Assign a model to the TreeNode
        public GMDL.model model;

        public MyTreeNode(string name)
        {
            this.Text = name;
        }
    }

    public class NoClickTree : TreeView
    {
        protected override void WndProc(ref Message m)
        {
            // Suppress WM_LBUTTONDBLCLK
            if (m.Msg == 0x203) { m.Result = IntPtr.Zero; }
            else base.WndProc(ref m);
        }
    };
}
