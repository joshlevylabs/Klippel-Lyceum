using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace LAPxv8
{
    public partial class BaseForm : Form
    {
        protected MenuStrip menuStrip;
        private bool showMenuStrip = true; // Default to true, but child classes can override this
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        public BaseForm(bool showMenu = true) // Allow child classes to disable menu
        {
            showMenuStrip = showMenu;
            InitializeBaseFormComponents();
        }

        protected void InitializeBaseFormComponents()
        {
            string basePath = Application.StartupPath;
            string iconPath = Path.Combine(basePath, "Resources", "LAPx.ico");

            // Set the favicon
            this.Icon = new Icon(iconPath);

            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark Mode background
            this.Padding = new Padding(1, 30, 1, 1);
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);



            if (showMenuStrip) // Only add menu if allowed
            {
                menuStrip = new MenuStrip
                {
                    Dock = DockStyle.Top,
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.White,
                    Padding = new Padding(0, 0, 0, 0)
                };

                var fileMenu = new ToolStripMenuItem("File");
                var logoutMenuItem = new ToolStripMenuItem("Logout"); // Changed from Home to Logout
                logoutMenuItem.Click += LogoutMenuItem_Click;
                fileMenu.DropDownItems.Add(logoutMenuItem);
                menuStrip.Items.Add(fileMenu);

                this.MainMenuStrip = menuStrip;
                this.Controls.Add(menuStrip);
            }

            this.Load += BaseForm_Load;
            this.Paint += BaseForm_Paint;

            // Ensure window remains draggable
            this.MouseDown += BaseForm_MouseDown;
            this.MouseMove += BaseForm_MouseMove;
            this.MouseUp += BaseForm_MouseUp;
        }
        private void LogoutMenuItem_Click(object sender, EventArgs e)
        {
            // Close all open forms
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                form.Invoke(new Action(() => form.Close()));
            }

            // Restart the application
            Application.ExitThread();
            System.Diagnostics.Process.Start(Application.ExecutablePath);
        }


        protected virtual void AddCustomMenuItems()
        {
            // This method is intended to be overridden in derived forms
        }

        private void HomeMenuItem_Click(object sender, EventArgs e)
        {
            var form1 = new Form1();
            form1.Show();
            this.Hide();
        }

        private void BaseForm_Load(object sender, EventArgs e)
        {
            // Only attach event handlers if menuStrip is not null
            if (menuStrip != null)
            {
                menuStrip.MouseDown += HeaderPanel_MouseDown;
                menuStrip.MouseMove += HeaderPanel_MouseMove;
                menuStrip.MouseUp += HeaderPanel_MouseUp;
            }

            // Call method to add custom menu items in derived forms
            AddCustomMenuItems();
        }

        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void HeaderPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }
        private void HeaderPanel_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
        }

        private void BaseForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void BaseForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }
        private void BaseForm_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private void BaseForm_Paint(object sender, PaintEventArgs e)
        {
            // Draw custom title bar
            Graphics g = e.Graphics;
            g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), new Rectangle(0, 0, this.Width, 30));

            // Draw icon
            g.DrawIcon(this.Icon, new Rectangle(5, 5, 20, 20));

            // Draw title
            g.DrawString(this.Text, new Font("Segoe UI", 12), Brushes.White, new Point(30, 5));

            // Draw close button
            g.FillRectangle(Brushes.Red, new Rectangle(this.Width - 30, 0, 30, 30));
            g.DrawString("X", new Font("Segoe UI", 12), Brushes.White, new Point(this.Width - 23, 5));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left)
            {
                // Check if the close button is clicked
                if (e.X >= this.Width - 30 && e.X <= this.Width && e.Y >= 0 && e.Y <= 30)
                {
                    this.Close();
                }
            }
        }

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect,
            int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Apply round corners to the form
            this.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 20, 20));
        }
    }
}