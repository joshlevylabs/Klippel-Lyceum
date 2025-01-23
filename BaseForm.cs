using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;

namespace LAPxv8
{
    public partial class BaseForm : Form
    {
        protected MenuStrip menuStrip;

        public BaseForm()
        {
            InitializeBaseFormComponents();
        }

        protected void InitializeBaseFormComponents()
        {
            // Assuming your executable is run from the bin\Debug or bin\Release folder,
            // and your images are copied to the same folder upon build
            string basePath = Application.StartupPath; // Gets the startup path of the application
            string iconPath = Path.Combine(basePath, "Resources", "LAPx.ico");

            // Set the favicon
            this.Icon = new Icon(iconPath);

            // Initialize and add the menu strip
            menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(30, 30, 30), // Dark Mode color
                ForeColor = Color.White,
                Padding = new Padding(0, 0, 0, 0)
            };

            // Add base menu items
            var fileMenu = new ToolStripMenuItem("File");
            var homeMenuItem = new ToolStripMenuItem("Home");
            homeMenuItem.Click += HomeMenuItem_Click;
            fileMenu.DropDownItems.Add(homeMenuItem);

            menuStrip.Items.Add(fileMenu);

            // Allow derived forms to add more menu items
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(45, 45, 45); // Dark Mode background
            this.Padding = new Padding(1, 30, 1, 1); // Adjusted padding for better look
            this.Load += BaseForm_Load;
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark mode background

            // To make the form draggable
            this.MouseDown += BaseForm_MouseDown;
            this.MouseMove += BaseForm_MouseMove;

            // Custom title bar paint event
            this.Paint += BaseForm_Paint;
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
            // Allow dragging of the form
            menuStrip.MouseDown += HeaderPanel_MouseDown;
            menuStrip.MouseMove += HeaderPanel_MouseMove;

            // Call method to add custom menu items in derived forms
            AddCustomMenuItems();
        }


        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

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