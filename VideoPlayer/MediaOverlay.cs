using Microsoft.VisualBasic.Devices;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace VideoPlayer
{
    public partial class MediaOverlay : Form
    {

        public enum ButtonStyle
        {
            Circle, Box
        }

        /// <summary>
        /// Draws a triangle within the given rectangle
        /// </summary>
        /// <param name="g"></param>
        /// <param name="rect"></param>
        /// <param name="fill"></param>
        /// <param name="scale"></param>
        static void DrawTriangle(Graphics g, RectangleF rect, Brush fill, float scale = 1f)
        {
            PointF[] PlayPoints = [
                    new(-0.18f  * scale * rect.Width, -0.31f * scale * rect.Height),
                    new(-0.18f  * scale * rect.Width, 0.31f  * scale * rect.Height),
                    new(0.33f   * scale * rect.Width, 0),
            ];
            g.ResetTransform();
            g.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            g.FillPolygon(fill, PlayPoints);
            g.ResetTransform();
            return;
        }


        /// <summary>
        /// Mouse delegate.
        /// </summary>
        /// <param name="x">relative X of cursor</param>
        /// <param name="y">relative Y of cursor</param>
        /// <param name="state">0 = none, 1 = pressed, 3 = just pressed </param>
        public delegate void Mouse(float x, float y, int state);

        /// <summary>
        /// Paints inside of the given 
        /// </summary>
        /// <param name="g"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        public delegate void Painter(Graphics g, MButton b, Brush s, float x, float y, float w, float h);
       
        public class MButton
        {
            public float RX = 0.5f, RY = 0.5f, RW = -1f, RH = 0.1f, OX = 0f, OY = 0f;
            public string Name;
            public ButtonStyle MyStyle = ButtonStyle.Circle;
            public event Mouse? OnClick;
            public event Mouse? OnHover;

            public event ActionHandler? DoAction;

            public event Painter? PaintStyle;
            public void Paint(Graphics g, Rectangle Bounds, Brush Fill, Brush Style, Pen Border)
            {
                //calculate my center
                float w = RW * Bounds.Width;
                float h = RH * Bounds.Height;

                if (RH < 0) h = w;
                else if (RW < 0) w = h;

                float cx = Bounds.X + RX * Bounds.Width + OX * Bounds.Height - w / 2;
                float cy = Bounds.Y + RY * Bounds.Height + OY * Bounds.Width - h / 2;

                //Fill the button
                switch(MyStyle)
                {
                    case ButtonStyle.Circle: g.FillEllipse(Fill, cx, cy, w, h); break;
                    case ButtonStyle.Box: g.FillRectangle(Fill, cx, cy, w, h); break;
                }

                //Draw the style
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                PaintStyle?.Invoke(g, this,  Style, cx, cy, w, h);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

                //Draw the border
                switch (MyStyle)
                {
                    case ButtonStyle.Circle: g.DrawEllipse(Border, cx, cy, w, h); break;
                    case ButtonStyle.Box: g.DrawRectangle(Border, cx, cy, w, h); break;
                }
            }

            public bool Contains(Rectangle bounds, Point cursor)
            {
                //calculate my center
                float w = RW * bounds.Width;
                float h = RH * bounds.Height;
                if (RH < 0) h = w;
                else if (RW < 0) w = h;
                float cx = bounds.X + RX * bounds.Width + OX * bounds.Height - w / 2;
                float cy = bounds.Y + RY * bounds.Height + OY * bounds.Width - h / 2;
                float dx = cx + (w/2) - cursor.X;
                float dy = cy + (h/2) - cursor.Y;
                switch(MyStyle)
                {
                    case ButtonStyle.Circle: return (dx * dx + dy * dy) < (w * w / 4f);
                    case ButtonStyle.Box: return new RectangleF(cx, cy, w, h).Contains(cursor);
                }
                return false;
            }

            public void TryClick(Rectangle bounds, Point cursor)
            {
                if (!Contains(bounds, cursor)) return;
                OnClick?.Invoke(cursor.X / (float)bounds.Width, cursor.Y / (float) bounds.Height, 3);
            }


        }

        public delegate void ActionHandler(double dN);
        public event ActionHandler? PlayPauseClick;
        public event ActionHandler? StepForwardClick;
        public event ActionHandler? StepBackwardClick;
        public event ActionHandler? NextClick;
        public event ActionHandler? PreviousClick;
        public event ActionHandler? TrackBarClick;

        /// <summary>
        /// The form that owns the control we are bound to
        /// </summary>
        public Form? parentForm;

        /// <summary>
        /// The Control that we are bound to
        /// </summary>
        public Control parentControl;

        /// <summary>
        /// Callback to check if we are playing or not
        /// </summary>
        public Func<bool>? playingCallback;

        /// <summary>
        /// Callback to read the progress of the video
        /// </summary>
        public Func<double>? progressCallback;

        /// <summary>
        /// A list of the buttons in this controller
        /// </summary>
        public ICollection<MButton> Buttons = new List<MButton>();

        /// <summary>
        /// The button currently being hovered
        /// </summary>
        public MButton? Hovered = null;

        /// <summary>
        /// The desired height for the panel
        /// </summary>
        public double TargetHeight = 0.2d;

        /// <summary>
        /// Calculates the minimum height for this control panel
        /// </summary>
        private double MinHeight => 80 + (TargetHeight * 2 * Height / 3);
        /// <summary>
        /// The maximum opacity for the panel
        /// </summary>
        public double OpacityMax = 0.7;

        public double PopupDuration = 0.3d;
        public double HideDuration = 1d;
        private double _countdown = 1d;
        private double _waiter = 1d;
        public double HideWait = 0.5d;
        private bool IsSelector = false;
        public bool TrackOnly = false;

        public MediaOverlay(Control parent, Func<double>? ProgressPercent, Func<bool>? playingCallback)
        {

            if (parent.FindForm() == null) return;
            parentControl = parent;
            parentForm = parent.FindForm() ?? null;
            this.progressCallback = ProgressPercent;
            this.playingCallback = playingCallback;

            InitializeComponent();
            ConstructFormUI();
            SetFormProperties();
            SetFormEvents();





            this.Height = int.Max((int)MinHeight, (int)(TargetHeight * parentControl.Height));
            Parent_Move(this, new EventArgs());

            //set up the timer to keep us updated
            var t = new System.Windows.Forms.Timer();
            t.Interval = 10;
            Stopwatch s = Stopwatch.StartNew();
            t.Tick += (sed, e) =>
            {
                if (this.Disposing || this.IsDisposed) return;
                this.TopMost = false;
                this.TopMost = true;
                this.BringToFront();
                //compute the time taken
                double elapse = s.Elapsed.TotalSeconds;
                s.Restart();
                //Show the form if we contain the mouse, or the cursor, or the trackbar is showing
                if (Bounds.Contains(Cursor.Position) || IsSelector || TrackOnly)
                {
                    //When the keyboard selector is here, we clamp some waiting
                    if (!IsSelector) _waiter = double.Max(1, _waiter);
                    else _waiter -= elapse / HideWait; //otherwise there's a fade delay
                    //Become visible at the popup speed
                    _countdown += elapse / PopupDuration;
                }
                //countdown to hide
                else _waiter -= elapse / HideWait;
                //The wait timer hass expired, so start counting down
                if (_waiter < 0) _countdown -= elapse / HideDuration;
                //clamp the countdown
                _countdown = double.Clamp(_countdown, 0, 1);
                //now get the opacity and update us and so on
                this.Opacity = double.Max(0d, _countdown * OpacityMax);
                this.Invalidate();
                Parent_Move(null, new());
            };
            t.Start();



        }

        /// <summary>
        /// Sets a bunch of properties to make the form behave nicely
        /// </summary>
        public void SetFormProperties()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.AllowTransparency = true;
            this.TransparencyKey = Color.Violet;
            this.BackColor = TransparencyKey;
            this.DoubleBuffered = true;
            this.Visible = false;
        }

        /// <summary>
        /// Create the UI elements for the form (the media control buttons)
        /// </summary>
        public void ConstructFormUI()
        {

            //Create the play button
            MButton Play = new MButton()
            {
                Name = "play",
                MyStyle = ButtonStyle.Circle,
                RX = 0.5f,
                RY = 0.5f,
                RH = 0.65f,
            };
            Play.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                if (playingCallback?.Invoke() == true)
                {
                    float ww = w / 6f;
                    float ax = x + 0.3f * w - (ww / 2);
                    float bx = x + 0.7f * w - (ww / 2);
                    //draw two rectangles accordingly
                    g.FillRectangle(s, new RectangleF(ax, y + h * 0.2f, ww, h * 0.6f));
                    g.FillRectangle(s, new RectangleF(bx, y + h * 0.2f, ww, h * 0.6f));
                }
                else
                {
                    DrawTriangle(g, new(x, y, w, h), s);
                }
            };
            //and add the UI control
            Play.OnClick += (x, y, b) => PlayPauseClick?.Invoke(1d);

            //Create the forwards button
            MButton Forwards = new MButton()
            {
                Name = "forwards",
                MyStyle = ButtonStyle.Circle,
                RX = 0.5f,
                OX = 0.655f,
                RY = 0.5f,
                RH = 0.55f,
            };
            Forwards.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                //draw the two triangles for the 1>1> thing
                DrawTriangle(g, new(x - w / 6, y, w, h), s, 0.7f);
                DrawTriangle(g, new(x + w / 6, y, w, h), s, 0.7f);
            };
            //and add the UI controls
            Forwards.OnClick += (x, y, b) => StepForwardClick?.Invoke(10d);
            //same for the backwards button
            MButton Backwards = new MButton()
            {
                Name = "backwards",
                MyStyle = ButtonStyle.Circle,
                RX = 0.5f,
                OX = -0.655f,
                RY = 0.5f,
                RH = 0.55f,
            };
            Backwards.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                //draw the two triangles for the 1>1> thing
                DrawTriangle(g, new(x - w / 6, y, w, h), s, -0.7f);
                DrawTriangle(g, new(x + w / 6, y, w, h), s, -0.7f);
            };
            Backwards.OnClick += (x, y, b) => StepForwardClick?.Invoke(-10d);


            //Now we also need next/previous track buttons
            //Though these are currently not featured since there's no playlist
            MButton Next = new MButton()
            {
                Name = "next",
                MyStyle = ButtonStyle.Circle,
                RX = 0.5f,
                OX = 1.25f,
                RY = 0.5f,
                RH = 0.55f,
            };
            Next.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                DrawTriangle(g, new(x - w / 6, y, w, h), s, 0.8f);
                g.FillRectangle(s, x + 0.6f * w, y + 0.2f * h, w / 6, h * 0.6f);
            };
            Next.OnClick += (x, y, b) => NextClick?.Invoke(1d);

            MButton Prev = new MButton()
            {
                Name = "previous",
                MyStyle = ButtonStyle.Circle,
                RX = 0.5f,
                OX = -1.25f,
                RY = 0.5f,
                RH = 0.55f,
            };
            Prev.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                DrawTriangle(g, new(x + w / 6, y, w, h), s, -0.8f);
                g.FillRectangle(s, x + 0.2f * w, y + 0.2f * h, w / 6, h * 0.6f);
            };
            Prev.OnClick += (x, y, b) => PreviousClick?.Invoke(-1d);


            //Lastly, we also need a trackbar for controlling the video postion
            MButton Trackbar = new MButton()
            {
                Name = "trackbar",
                MyStyle = ButtonStyle.Box,
                RX = 0.5f,
                RY = 0.05f,
                RH = 0.12f,
                RW = 0.7f
            };
            Trackbar.PaintStyle += (g, b, s, x, y, w, h) =>
            {
                g.FillRectangle(s, x, y, w * (float)(progressCallback?.Invoke() ?? 0), h);
            };
            Trackbar.OnClick += (x, y, b) =>
            {
                //ugh i cri
                x *= ClientRectangle.Width;
                //calculate my center
                float w = Trackbar.RW * ClientRectangle.Width;
                float cx = ClientRectangle.X + Trackbar.RX * ClientRectangle.Width - w / 2;
                TrackBarClick?.Invoke(double.Clamp((x - cx) / w, 0, 1));
            };


            //Now add the controls to the control collection
            Buttons.Add(Play);
            Buttons.Add(Backwards);
            Buttons.Add(Forwards);
            Buttons.Add(Next);
            Buttons.Add(Prev);
            Buttons.Add(Trackbar);

            //Let's just stick a thing here to make it render them
            Paint += (s, e) =>
            {
                using Pen p = new Pen(Color.White, 2f);
                foreach (var t in Buttons)
                {
                    t.Paint(e.Graphics, ClientRectangle,
                        (t == Hovered) ? Brushes.Gray : Brushes.DarkGray,
                        Brushes.White,
                        (t == Hovered) ? p : Pens.White);
                }
            };

        }

        /// <summary>
        /// Set the events for the form, which make it move around and hide and so on correctly
        /// </summary>
        public void SetFormEvents()
        {
            if (parentForm != null)
            {
                parentForm.FormClosed += Parent_FormClosed;
                parentForm.Move += Parent_Move;
                parentForm.Resize += Parent_Move;
                
                if (parentForm is VLCPlayerWindow vp)
                {
                    PlayPauseClick += (d) => vp.PlayPause();
                    StepForwardClick += (d) => vp.Position += TimeSpan.FromSeconds(d);
                    StepBackwardClick += (d) => vp.Position += TimeSpan.FromSeconds(d);
                    TrackBarClick += (d) => vp.PositionFrac = d;
                }
            }

            if (parentControl != null)
            {
                parentControl.Move += Parent_Move;
                parentControl.Resize += Parent_Move;
                parentControl.Disposed += Parent_Disposed;
            }

            Resize += Parent_Move;
            MouseDown += FormMouseDown;
            MouseMove += FormMouseMove;
            MouseLeave += FormMouseLeave;

        }


        private void FormMouseLeave(object? sender, EventArgs e)
        {
            Hovered = null;
        }

        private void FormMouseMove(object? sender, MouseEventArgs e)
        {
            MButton? under = null;
            foreach (MButton button in Buttons)
            {
                if (button.Contains(this.ClientRectangle, e.Location))
                {
                    under = button;
                    break;
                }
            }
            if (under != Hovered)
            {
                Hovered = under;
                Invalidate();
            }
        }

        private void FormMouseDown(object? sender, MouseEventArgs e)
        {
            Hovered = null;
            foreach (MButton button in Buttons)
            {
                if (button.Contains(this.ClientRectangle, e.Location))
                {
                    Hovered = button;
                    button.TryClick(ClientRectangle, e.Location);
                    break;
                }
            }

            Invalidate();
        }


        private void Parent_Disposed(object? sender, EventArgs e)
        {
            try
            {
                this.Close();
                this.Dispose();
            }
            catch { }
        }

        private void Parent_Move(object? sender, EventArgs e)
        {
            //check that we're fine to do this
            try
            {
                if (this.Disposing || this.IsDisposed) return;
                if (parentControl == null || parentControl.IsDisposed)
                {
                    this.Close();
                    return;
                }
                var p = parentControl?.PointToScreen(Point.Empty);
                this.Height = int.Max((int)MinHeight, (int)(TargetHeight * parentControl.Height));
                this.Location = new(p.Value.X,
                    p.Value.Y + parentControl.Height - this.Height
                    );

                this.Width = parentControl.Width;
                this.Visible = parentForm.Visible && parentForm.WindowState != FormWindowState.Minimized;
            }
            catch { }
        }

        private void Parent_FormClosed(object? sender, FormClosedEventArgs e)
        {
            try
            {
                this.Close();
                this.Dispose();
            }
            catch
            {

            }

        }


    }
}
