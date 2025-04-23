using System;
using System.Windows.Forms;
using System.Drawing;
using System.Data;
using System.Data.SQLite;
using static LyceumKlippel.LYKHome;

namespace LyceumKlippel
{
    public partial class QueryDatabaseForm : BaseForm
    {
        private string dbFilePath;
        private TextBox txtQuery;
        private Button btnExecute;
        private DataGridView dgvResults;
        private TreeView treeViewSchema;

        public QueryDatabaseForm(string databasePath) : base(showMenu: false, token: null)
        {
            dbFilePath = databasePath;
            InitializeComponent();
            LoadDatabaseSchema();

            treeViewSchema.AfterSelect += TreeViewSchema_AfterSelect;
        }

        private void InitializeComponent()
        {
            this.Text = "Query Database";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(45, 45, 45); // Match dark theme

            // Main SplitContainer
            SplitContainer splitContainerMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.FromArgb(45, 45, 45),
                SplitterWidth = 5
            };

            // Left panel: TreeView for schema
            treeViewSchema = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            treeViewSchema.DoubleClick += TreeViewSchema_DoubleClick;
            splitContainerMain.Panel1.Controls.Add(treeViewSchema);

            // Right panel: another SplitContainer for query and results
            SplitContainer splitContainerRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.FromArgb(45, 45, 45),
                SplitterWidth = 5
            };

            // Top part of right panel: Panel for query TextBox and button
            Panel panelQuery = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45)
            };

            txtQuery = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Top,
                Height = 100,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };
            panelQuery.Controls.Add(txtQuery);

            btnExecute = new Button
            {
                Text = "Execute",
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.Click += BtnExecute_Click;
            panelQuery.Controls.Add(btnExecute);

            splitContainerRight.Panel1.Controls.Add(panelQuery);

            // Bottom part of right panel: DataGridView for results
            dgvResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                GridColor = Color.Gray,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgvResults.DataError += DgvResults_DataError;
            splitContainerRight.Panel2.Controls.Add(dgvResults);

            // Add the inner SplitContainer to the main SplitContainer's right panel
            splitContainerMain.Panel2.Controls.Add(splitContainerRight);

            // Add the main SplitContainer to the form
            this.Controls.Add(splitContainerMain);
        }

        private void LoadDatabaseSchema()
        {
            treeViewSchema.Nodes.Clear();
            using (SQLiteConnection conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;Read Only=True;"))
            {
                conn.Open();
                DataTable schema = conn.GetSchema("Tables");
                foreach (DataRow row in schema.Rows)
                {
                    string tableName = row["TABLE_NAME"].ToString();
                    TreeNode tableNode = new TreeNode(tableName) { Tag = tableName };
                    treeViewSchema.Nodes.Add(tableNode);

                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA table_info({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string columnName = reader["name"].ToString();
                                string columnType = reader["type"].ToString();
                                TreeNode columnNode = new TreeNode($"{columnName} ({columnType})") { Tag = columnName };
                                tableNode.Nodes.Add(columnNode);
                            }
                        }
                    }
                }
            }
        }

        private void TreeViewSchema_DoubleClick(object sender, EventArgs e)
        {
            if (treeViewSchema.SelectedNode != null && treeViewSchema.SelectedNode.Parent == null)
            {
                string tableName = treeViewSchema.SelectedNode.Text;
                txtQuery.Text = $"SELECT * FROM {tableName};";
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(txtQuery.Text, conn))
                    {
                        using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            DataTable dtDisplay = PrepareDataTableForDisplay(dt);
                            dgvResults.DataSource = dtDisplay;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing query: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DataTable PrepareDataTableForDisplay(DataTable dt)
        {
            DataTable dtDisplay = new DataTable();

            foreach (DataColumn col in dt.Columns)
            {
                if (col.DataType == typeof(byte[]))
                {
                    dtDisplay.Columns.Add(col.ColumnName, typeof(string));
                }
                else
                {
                    dtDisplay.Columns.Add(col.ColumnName, col.DataType);
                }
            }

            foreach (DataRow row in dt.Rows)
            {
                DataRow newRow = dtDisplay.NewRow();
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (dt.Columns[i].DataType == typeof(byte[]))
                    {
                        byte[] data = row[i] as byte[];
                        newRow[i] = data != null ? $"Binary data (length: {data.Length})" : "NULL";
                    }
                    else
                    {
                        newRow[i] = row[i];
                    }
                }
                dtDisplay.Rows.Add(newRow);
            }

            return dtDisplay;
        }

        private void DgvResults_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            LogManager.AppendLog($"DataGridView DataError: {e.Exception.Message}, Context: {e.Context}");
            e.ThrowException = false;
        }

        private string GetTableDetails(string tableName)
        {
            string details = $"Table: {tableName}\n";
            if (tableName == "sqlite_sequence")
            {
                details += "Note: This is an internal SQLite table used to manage auto-incrementing sequences for PRIMARY KEY columns.\n\n";
            }
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    // Get the SQL used to create the table
                    using (SQLiteCommand cmd = new SQLiteCommand($"SELECT sql FROM sqlite_master WHERE type='table' AND name='{tableName}';", conn))
                    {
                        string createSql = cmd.ExecuteScalar()?.ToString();
                        if (!string.IsNullOrEmpty(createSql))
                        {
                            details += $"CREATE TABLE statement:\n{createSql}\n\n";
                        }
                        else
                        {
                            details += "CREATE TABLE statement not found.\n\n";
                        }
                    }
                    details += "----------------------------------------\n";
                    // Get number of rows
                    using (SQLiteCommand cmd = new SQLiteCommand($"SELECT COUNT(*) FROM {tableName};", conn))
                    {
                        long rowCount = (long)cmd.ExecuteScalar();
                        details += $"Number of rows: {rowCount}\n\n";
                    }
                    details += "----------------------------------------\n";
                    // Get columns
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA table_info({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            details += "Columns:\n";
                            while (reader.Read())
                            {
                                string colName = reader["name"].ToString();
                                string colType = reader["type"].ToString();
                                string notNull = reader["notnull"].ToString() == "1" ? "NOT NULL" : "";
                                string pk = reader["pk"].ToString() == "1" ? "PRIMARY KEY" : "";
                                string defaultValue = reader["dflt_value"].ToString();
                                details += $"- {colName} ({colType}) {notNull} {pk}";
                                if (!string.IsNullOrEmpty(defaultValue))
                                {
                                    details += $" DEFAULT {defaultValue}";
                                }
                                details += "\n";
                            }
                            details += "\n";
                        }
                    }
                    details += "----------------------------------------\n";
                    // Get indexes
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA index_list({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            details += "Indexes:\n";
                            while (reader.Read())
                            {
                                string indexName = reader["name"].ToString();
                                string unique = reader["unique"].ToString() == "1" ? "UNIQUE" : "";
                                details += $"- {indexName} {unique}\n";
                            }
                            details += "\n";
                        }
                    }
                    details += "----------------------------------------\n";
                    // Get foreign keys
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA foreign_key_list({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            details += "Foreign Keys:\n";
                            while (reader.Read())
                            {
                                string fromColumn = reader["from"].ToString();
                                string toTable = reader["table"].ToString();
                                string toColumn = reader["to"].ToString();
                                details += $"- {fromColumn} -> {toTable}.{toColumn}\n";
                            }
                            details += "\n";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                details += $"Error: {ex.Message}\n";
            }
            return details;
        }
        private string GetColumnDetails(string tableName, string columnName)
        {
            string details = $"Column: {columnName} in Table: {tableName}\n";
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    // Get basic column info
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA table_info({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["name"].ToString() == columnName)
                                {
                                    string colType = reader["type"].ToString();
                                    string notNull = reader["notnull"].ToString() == "1" ? "Yes" : "No";
                                    string pk = reader["pk"].ToString() == "1" ? "Yes" : "No";
                                    string defaultValue = reader["dflt_value"].ToString();
                                    details += $"Data Type: {colType}\n";
                                    details += $"Not Null: {notNull}\n";
                                    details += $"Primary Key: {pk}\n";
                                    if (!string.IsNullOrEmpty(defaultValue))
                                        details += $"Default Value: {defaultValue}\n";
                                    break;
                                }
                            }
                        }
                    }
                    // Check if the column is part of any indexes
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA index_list({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string indexName = reader["name"].ToString();
                                using (SQLiteCommand cmdIndex = new SQLiteCommand($"PRAGMA index_info({indexName});", conn))
                                {
                                    using (SQLiteDataReader readerIndex = cmdIndex.ExecuteReader())
                                    {
                                        while (readerIndex.Read())
                                        {
                                            if (readerIndex["name"].ToString() == columnName)
                                            {
                                                details += $"Part of Index: {indexName}\n";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Check if the column is a foreign key
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA foreign_key_list({tableName});", conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["from"].ToString() == columnName)
                                {
                                    string toTable = reader["table"].ToString();
                                    string toColumn = reader["to"].ToString();
                                    details += $"Foreign Key to: {toTable}.{toColumn}\n";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                details += $"Error: {ex.Message}\n";
            }
            return details;
        }
        private void TreeViewSchema_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string details = "";
            if (e.Node.Parent == null) // Table selected
            {
                string tableName = e.Node.Tag.ToString();
                details = GetTableDetails(tableName);
            }
            else // Column selected
            {
                string tableName = e.Node.Parent.Tag.ToString();
                string columnName = e.Node.Tag.ToString();
                details = GetColumnDetails(tableName, columnName);
            }

            if (!string.IsNullOrEmpty(details))
            {
                LogManager.AppendLog(details);
            }
        }
    }
}