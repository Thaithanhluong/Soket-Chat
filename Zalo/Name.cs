using System;
using System.Windows.Forms;

namespace Zalo
{
    public partial class Name : Form
    {
        public Name()
        {
            InitializeComponent();
        }

        private void btnConfirm_Click(object sender, EventArgs e)
        {
            string name = txtMessage.Text;
            Client client = new Client(name);
            client.Show();
            Hide();
        }
    }
}
