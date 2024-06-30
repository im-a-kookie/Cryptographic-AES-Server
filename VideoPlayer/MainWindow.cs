
using System.Security.Cryptography;

namespace VideoPlayer
{
    public partial class PlayerExampleForm : Form
    {
        public PlayerExampleForm()
        {
            InitializeComponent();

            tbInput.TextChanged += (s, e) =>
            {
                if (File.Exists(tbInput.Text))
                {
                    tbOutput.Text = Path.ChangeExtension(tbInput.Text, ".emkv");
                }
            };

            bOpen.Click += (s, e) =>
            {
                string filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv|All Files (*.*)|*.*";
                using (OpenFileDialog ofd = new OpenFileDialog() { Filter = filter })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        tbInput.Text = ofd.FileName;
                    }
                }
            };

            bEncrypt.Click += (s, e) =>
            {
                if (File.Exists(tbInput.Text))
                {
                    tbOutput.Text = Path.ChangeExtension(tbInput.Text, ".emkv");
                    AesHelper.EncryptFile(tbInput.Text, tbOutput.Text, "default");
                    string url = VideoServer.StartStreamingFile(tbOutput.Text);
                    var player = new VLCPlayerWindow(url);
                    player.Show();
                }
            };
        }

    }
}
