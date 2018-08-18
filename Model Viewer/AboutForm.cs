using System.Windows.Forms;

namespace Model_Viewer
{
    public partial class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
            versionLabel.Text = Util.Version + " - NMS NEXT HEAVY TESTING";
            wikilabel.Links.Add(0, wikilabel.Text.Length, "https://bitbucket.org/gregkwaste/nms-viewer/wiki/Home");
            repolabel.Links.Add(0, repolabel.Text.Length, "https://bitbucket.org/gregkwaste/nms-viewer");
            donatelabel.Links.Add(0, donatelabel.Text.Length, "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=arianos10@gmail.com&lc=GR&item_name=3dgamedevblog&currency_code=EUR&bn=PP-DonationsBF:btn_donateCC_LG.gif:NonHosted");
            bloglabel.Links.Add(0, bloglabel.Text.Length, "https://3dgamedevblog.com");
        }

        private void wikilabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }
    }
}
