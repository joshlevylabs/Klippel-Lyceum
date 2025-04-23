using System.Runtime.InteropServices;

namespace LyceumKlippel
{
    partial class LYKHome
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Release COM objects if they exist
                if (currentQcNode != null && currentQcModule != null)
                {
                    currentQcNode.ReleaseInstance();
                    Marshal.ReleaseComObject(currentQcModule);
                    currentQcModule = null;
                    Marshal.ReleaseComObject(currentQcNode);
                    currentQcNode = null;
                }
                if (database != null)
                {
                    database.Close();
                    Marshal.ReleaseComObject(database);
                    database = null;
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>


        #endregion
    }
}