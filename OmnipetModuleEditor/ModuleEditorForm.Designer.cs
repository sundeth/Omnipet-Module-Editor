namespace OmnipetModuleEditor
{
    partial class ModuleEditorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonReport;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.Button buttonGenerateDoc;
        private System.Windows.Forms.Button buttonOpenDoc;
        private System.Windows.Forms.Button buttonAccount;
        private System.Windows.Forms.Button buttonPublish;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonReport = new System.Windows.Forms.Button();
            this.buttonExport = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonGenerateDoc = new System.Windows.Forms.Button();
            this.buttonOpenDoc = new System.Windows.Forms.Button();
            this.buttonAccount = new System.Windows.Forms.Button();
            this.buttonPublish = new System.Windows.Forms.Button();

            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControlMain
            // 
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 0);
            this.tabControlMain.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1125, 763);
            this.tabControlMain.TabIndex = 0;
            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.buttonGenerateDoc);
            this.panelBottom.Controls.Add(this.buttonOpenDoc);
            this.panelBottom.Controls.Add(this.buttonAccount);
            this.panelBottom.Controls.Add(this.buttonPublish);
            this.panelBottom.Controls.Add(this.buttonExport);
            this.panelBottom.Controls.Add(this.buttonReport);
            this.panelBottom.Controls.Add(this.buttonSave);
            this.panelBottom.Controls.Add(this.buttonCancel);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 763);
            this.panelBottom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1125, 49);
            this.panelBottom.TabIndex = 1;
            // 
            // buttonGenerateDoc
            // 
            this.buttonGenerateDoc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonGenerateDoc.Location = new System.Drawing.Point(12, 12);
            this.buttonGenerateDoc.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonGenerateDoc.Name = "buttonGenerateDoc";
            this.buttonGenerateDoc.Size = new System.Drawing.Size(160, 24);
            this.buttonGenerateDoc.TabIndex = 2;
            this.buttonGenerateDoc.Text = "Generate Documentation";
            this.buttonGenerateDoc.UseVisualStyleBackColor = true;
            this.buttonGenerateDoc.Click += new System.EventHandler(this.buttonGenerateDoc_Click);
            // 
            // buttonOpenDoc
            // 
            this.buttonOpenDoc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonOpenDoc.Location = new System.Drawing.Point(180, 12);
            this.buttonOpenDoc.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonOpenDoc.Name = "buttonOpenDoc";
            this.buttonOpenDoc.Size = new System.Drawing.Size(140, 24);
            this.buttonOpenDoc.TabIndex = 5;
            this.buttonOpenDoc.Text = "Open Documentation";
            this.buttonOpenDoc.UseVisualStyleBackColor = true;
            this.buttonOpenDoc.Click += new System.EventHandler(this.buttonOpenDoc_Click);
            // 
            // buttonAccount
            // 
            this.buttonAccount.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.buttonAccount.Location = new System.Drawing.Point(450, 12);
            this.buttonAccount.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonAccount.Name = "buttonAccount";
            this.buttonAccount.Size = new System.Drawing.Size(100, 24);
            this.buttonAccount.TabIndex = 3;
            this.buttonAccount.Text = "Account";
            this.buttonAccount.UseVisualStyleBackColor = true;
            this.buttonAccount.Click += new System.EventHandler(this.buttonAccount_Click);
            // 
            // buttonPublish
            // 
            this.buttonPublish.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.buttonPublish.Location = new System.Drawing.Point(560, 12);
            this.buttonPublish.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonPublish.Name = "buttonPublish";
            this.buttonPublish.Size = new System.Drawing.Size(100, 24);
            this.buttonPublish.TabIndex = 4;
            this.buttonPublish.Text = "Publish";
            this.buttonPublish.UseVisualStyleBackColor = true;
            this.buttonPublish.Click += new System.EventHandler(this.buttonPublish_Click);
            // 
            // buttonReport
            // 
            this.buttonReport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReport.Location = new System.Drawing.Point(870, 12);
            this.buttonReport.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonReport.Name = "buttonReport";
            this.buttonReport.Size = new System.Drawing.Size(80, 24);
            this.buttonReport.TabIndex = 6;
            this.buttonReport.Text = "Report";
            this.buttonReport.UseVisualStyleBackColor = true;
            this.buttonReport.Click += new System.EventHandler(this.buttonReport_Click);
            // 
            // buttonExport
            // 
            this.buttonExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonExport.Location = new System.Drawing.Point(781, 12);
            this.buttonExport.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(80, 24);
            this.buttonExport.TabIndex = 7;
            this.buttonExport.Text = "Export";
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // buttonSave
            // 
            this.buttonSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSave.Location = new System.Drawing.Point(960, 12);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(75, 24);
            this.buttonSave.TabIndex = 0;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.Location = new System.Drawing.Point(1042, 12);
            this.buttonCancel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 24);
            this.buttonCancel.TabIndex = 1;
            this.buttonCancel.Text = "Close";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
            // 
            // ModuleEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1125, 812);
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.panelBottom);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "ModuleEditorForm";
            this.Text = "Module Editor";
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}