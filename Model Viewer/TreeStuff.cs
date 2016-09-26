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
}
