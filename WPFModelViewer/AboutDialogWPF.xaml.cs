using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WPFModelViewer
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();

            //Override version
            Version.Text = Util.getVersion();
            DonateLink.NavigateUri =  new Uri(Util.donateLink);
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            //Assuming that the sender is a hyperlink object
            Hyperlink h = (Hyperlink)sender;

            System.Diagnostics.Process.Start(h.NavigateUri.ToString());
        }
    }
}
