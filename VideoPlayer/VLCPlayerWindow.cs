using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoPlayer
{
    public partial class VLCPlayerWindow : Form
    {
        private LibVLCSharp.Shared.LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;

        /// <summary>
        /// We need this to make the play button work correctly after the media track ends
        /// </summary>
        float _oldPosition = -1;

        public double PositionFrac
        {
            get
            {
                return _mediaPlayer.Position;
            }
            set
            {
                _mediaPlayer.Position = (float)double.Clamp(value, 0, 1);
                _oldPosition = _mediaPlayer.Position;
            }
        }

        public TimeSpan Position
        {
            get {
                if (_mediaPlayer.Media == null) return TimeSpan.Zero;
                var d = _mediaPlayer.Media.Duration;
                if (d < 0) return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(_mediaPlayer.Position * (double)d);
            }
            set
            {
                if (_mediaPlayer.Media == null) return;
                var d = _mediaPlayer.Media.Duration;
                if (d <= 0) return;
                _mediaPlayer.Position = (float)(value.TotalMilliseconds / (float)_mediaPlayer.Media.Duration);
            }
        }

        public void Play()
        {
            if (_mediaPlayer.Media == null) return;
            _mediaPlayer.Play();
        }

        public void Pause()
        {
            if (_mediaPlayer.Media == null) return;
            _mediaPlayer.Pause();
        }

        public void PlayPause()
        {
            if (_mediaPlayer.Media == null) 
                return;
            if (_mediaPlayer.Media.State == VLCState.Ended)
            {
                var m = _mediaPlayer.Media.Duplicate();
                _mediaPlayer.Media = m;

                //EventHandler<MediaPlayerPositionChangedEventArgs>? x = null;
                //x = (s, e) => { 
                //    _mediaPlayer.PositionChanged -= x; 
                //    _mediaPlayer.Position = _oldPosition; 
                //};

                //_mediaPlayer.PositionChanged += x;
                _mediaPlayer.Play();
                _mediaPlayer.Position = _oldPosition;
                return;
            }

            if (_mediaPlayer.IsPlaying) _mediaPlayer.Pause();
            else _mediaPlayer.Play();
        }

        public int Volume
        {
            get {
                return _mediaPlayer.Volume;
            }
            set {
                _mediaPlayer.Volume = int.Clamp(value, 0, 100);
            }
        }

        public void Mute()
        {
            _mediaPlayer.Mute = true;
        }

        public void UnMute()
        {
            _mediaPlayer.Mute = false;
        }

        public void ToggleMute()
        {
            _mediaPlayer.Mute = !_mediaPlayer.Mute;
        }

        public VLCPlayerWindow(string url)
        {
            InitializeComponent();

            _libVLC = new();
            _mediaPlayer = new(_libVLC);

            var vw = new VideoView
            {
                MediaPlayer = _mediaPlayer,
                Dock = DockStyle.Fill
            };

            this.Controls.Add(vw);
            int W = 320;
            vw.Size = new Size(W, (W * 9) / 16);
            vw.Location = new Point(0, 0);

            using var media = new Media(_libVLC, url, File.Exists(url) ? FromType.FromPath : FromType.FromLocation);
            _mediaPlayer.Play(media);

            var controler = new MediaOverlay(vw, () => _mediaPlayer.Position, () => _mediaPlayer.IsPlaying);
            controler.Owner = this;
            controler.Show();

        }



    }
}
