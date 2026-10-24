namespace OmnipetModuleEditor
{
    partial class ModuleEditorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.MenuStrip menuStripMain;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuSave;
        private System.Windows.Forms.ToolStripMenuItem menuClose;
        private System.Windows.Forms.ToolStripMenuItem menuDocumentation;
        private System.Windows.Forms.ToolStripMenuItem menuCreateDoc;
        private System.Windows.Forms.ToolStripMenuItem menuOpenDoc;
        private System.Windows.Forms.ToolStripMenuItem menuTools;
        private System.Windows.Forms.ToolStripMenuItem menuModuleReport;
        private System.Windows.Forms.ToolStripMenuItem menuExportSprites;
        private System.Windows.Forms.ToolStripMenuItem menuGeneratePetIndex;
        private System.Windows.Forms.ToolStripMenuItem menuImportCollection;
        private System.Windows.Forms.ToolStripMenuItem menuOmninet;
        private System.Windows.Forms.ToolStripMenuItem menuAccount;
        private System.Windows.Forms.ToolStripMenuItem menuManageModule;
        private System.Windows.Forms.ToolStripMenuItem menuManageModules;
        private System.Windows.Forms.ToolStripMenuItem menuEdit;
        private System.Windows.Forms.ToolStripMenuItem menuEditEvolutions;
        private System.Windows.Forms.ToolStripMenuItem menuEnemyEditor;
        private System.Windows.Forms.ToolStripMenuItem menuSpecialEncounters;
        private System.Windows.Forms.ToolStripMenuItem menuUpdateAtkSprites;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonDb;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonRecordMatch;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonValidateNames;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonNormalizeNames;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonImportMinWeight;
        private System.Windows.Forms.ToolStripMenuItem menuDigimonUpdateSprites;

        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button buttonCancel;

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
            this.menuStripMain = new System.Windows.Forms.MenuStrip();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuClose = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDocumentation = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCreateDoc = new System.Windows.Forms.ToolStripMenuItem();
            this.menuOpenDoc = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTools = new System.Windows.Forms.ToolStripMenuItem();
            this.menuModuleReport = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExportSprites = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGeneratePetIndex = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImportCollection = new System.Windows.Forms.ToolStripMenuItem();
            this.menuOmninet = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAccount = new System.Windows.Forms.ToolStripMenuItem();
            this.menuManageModule = new System.Windows.Forms.ToolStripMenuItem();
            this.menuManageModules = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEditEvolutions = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEnemyEditor = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSpecialEncounters = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUpdateAtkSprites = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonDb = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonRecordMatch = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonValidateNames = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonNormalizeNames = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonImportMinWeight = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDigimonUpdateSprites = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.buttonSave = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();

            this.menuStripMain.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStripMain
            //
            this.menuStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuFile,
                this.menuEdit,
                this.menuDocumentation,
                this.menuTools,
                this.menuOmninet,
                this.menuDigimonDb});
            this.menuStripMain.Location = new System.Drawing.Point(0, 0);
            this.menuStripMain.Name = "menuStripMain";
            this.menuStripMain.Size = new System.Drawing.Size(1125, 24);
            this.menuStripMain.TabIndex = 0;
            this.menuStripMain.Text = "menuStripMain";
            //
            // menuFile
            //
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuSave,
                this.menuClose});
            this.menuFile.Name = "menuFile";
            this.menuFile.Size = new System.Drawing.Size(37, 20);
            this.menuFile.Text = "File";
            //
            // menuSave
            //
            this.menuSave.Name = "menuSave";
            this.menuSave.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.menuSave.Size = new System.Drawing.Size(180, 22);
            this.menuSave.Text = "Save";
            this.menuSave.Click += new System.EventHandler(this.buttonSave_Click);
            //
            // menuClose
            //
            this.menuClose.Name = "menuClose";
            this.menuClose.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.menuClose.Size = new System.Drawing.Size(180, 22);
            this.menuClose.Text = "Close";
            this.menuClose.Click += new System.EventHandler(this.buttonCancel_Click);
            //
            // menuDocumentation
            //
            this.menuDocumentation.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuCreateDoc,
                this.menuOpenDoc});
            this.menuDocumentation.Name = "menuDocumentation";
            this.menuDocumentation.Size = new System.Drawing.Size(102, 20);
            this.menuDocumentation.Text = "Documentation";
            //
            // menuCreateDoc
            //
            this.menuCreateDoc.Name = "menuCreateDoc";
            this.menuCreateDoc.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.G)));
            this.menuCreateDoc.Size = new System.Drawing.Size(200, 22);
            this.menuCreateDoc.Text = "Create Documentation";
            this.menuCreateDoc.Click += new System.EventHandler(this.buttonGenerateDoc_Click);
            //
            // menuOpenDoc
            //
            this.menuOpenDoc.Name = "menuOpenDoc";
            this.menuOpenDoc.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.G)));
            this.menuOpenDoc.Size = new System.Drawing.Size(200, 22);
            this.menuOpenDoc.Text = "Open Documentation";
            this.menuOpenDoc.Click += new System.EventHandler(this.buttonOpenDoc_Click);
            //
            // menuTools
            //
            this.menuTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuModuleReport,
                this.menuExportSprites,
                this.menuUpdateAtkSprites,
                this.menuGeneratePetIndex,
                this.menuImportCollection});
            this.menuTools.Name = "menuTools";
            this.menuTools.Size = new System.Drawing.Size(46, 20);
            this.menuTools.Text = "Tools";
            //
            // menuModuleReport
            //
            this.menuModuleReport.Name = "menuModuleReport";
            this.menuModuleReport.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.menuModuleReport.Size = new System.Drawing.Size(180, 22);
            this.menuModuleReport.Text = "Module Report";
            this.menuModuleReport.Click += new System.EventHandler(this.buttonReport_Click);
            //
            // menuExportSprites
            //
            this.menuExportSprites.Name = "menuExportSprites";
            this.menuExportSprites.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.E)));
            this.menuExportSprites.Size = new System.Drawing.Size(180, 22);
            this.menuExportSprites.Text = "Export Sprites";
            this.menuExportSprites.Click += new System.EventHandler(this.buttonExport_Click);
            //
            // menuGeneratePetIndex
            //
            this.menuGeneratePetIndex.Name = "menuGeneratePetIndex";
            this.menuGeneratePetIndex.Size = new System.Drawing.Size(180, 22);
            this.menuGeneratePetIndex.Text = "Generate Pet Index";
            this.menuGeneratePetIndex.Click += new System.EventHandler(this.generatePetIndex_Click);
            //
            // menuImportCollection
            //
            this.menuImportCollection.Name = "menuImportCollection";
            this.menuImportCollection.Size = new System.Drawing.Size(220, 22);
            this.menuImportCollection.Text = "Import Collection from Module";
            this.menuImportCollection.Click += new System.EventHandler(this.importCollection_Click);
            //
            // menuOmninet
            //
            this.menuOmninet.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuAccount,
                this.menuManageModule,
                this.menuManageModules});
            this.menuOmninet.Name = "menuOmninet";
            this.menuOmninet.Size = new System.Drawing.Size(62, 20);
            this.menuOmninet.Text = "Omninet";
            //
            // menuAccount
            //
            this.menuAccount.Name = "menuAccount";
            this.menuAccount.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.A)));
            this.menuAccount.Size = new System.Drawing.Size(180, 22);
            this.menuAccount.Text = "Account";
            this.menuAccount.Click += new System.EventHandler(this.buttonAccount_Click);
            //
            // menuManageModule
            //
            this.menuManageModule.Name = "menuManageModule";
            this.menuManageModule.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.M)));
            this.menuManageModule.Size = new System.Drawing.Size(180, 22);
            this.menuManageModule.Text = "Manage Module";
            this.menuManageModule.Click += new System.EventHandler(this.manageModule_Click);
            //
            // menuManageModules
            //
            this.menuManageModules.Name = "menuManageModules";
            this.menuManageModules.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.L)));
            this.menuManageModules.Size = new System.Drawing.Size(180, 22);
            this.menuManageModules.Text = "Module Library";
            this.menuManageModules.Click += new System.EventHandler(this.manageModules_Click);
            //
            // menuEdit
            //
            this.menuEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuEditEvolutions,
                this.menuEnemyEditor,
                this.menuSpecialEncounters});
            this.menuEdit.Name = "menuEdit";
            this.menuEdit.Size = new System.Drawing.Size(39, 20);
            this.menuEdit.Text = "Edit";
            //
            // menuEditEvolutions
            //
            this.menuEditEvolutions.Name = "menuEditEvolutions";
            this.menuEditEvolutions.Size = new System.Drawing.Size(180, 22);
            this.menuEditEvolutions.Text = "Edit Evolutions";
            this.menuEditEvolutions.Click += new System.EventHandler(this.editEvolutions_Click);
            //
            // menuEnemyEditor
            //
            this.menuEnemyEditor.Name = "menuEnemyEditor";
            this.menuEnemyEditor.Size = new System.Drawing.Size(180, 22);
            this.menuEnemyEditor.Text = "Enemy Editor";
            this.menuEnemyEditor.Click += new System.EventHandler(this.enemyEditor_Click);
            //
            // menuSpecialEncounters
            //
            this.menuSpecialEncounters.Name = "menuSpecialEncounters";
            this.menuSpecialEncounters.Size = new System.Drawing.Size(180, 22);
            this.menuSpecialEncounters.Text = "Special Encounters";
            this.menuSpecialEncounters.Click += new System.EventHandler(this.specialEncounters_Click);
            //
            // menuUpdateAtkSprites
            //
            this.menuUpdateAtkSprites.Name = "menuUpdateAtkSprites";
            this.menuUpdateAtkSprites.Size = new System.Drawing.Size(180, 22);
            this.menuUpdateAtkSprites.Text = "Update ATK Sprites";
            this.menuUpdateAtkSprites.Click += new System.EventHandler(this.updateAtkSprites_Click);
            //
            // menuDigimonDb
            //
            this.menuDigimonDb.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.menuDigimonRecordMatch,
                this.menuDigimonValidateNames,
                this.menuDigimonNormalizeNames,
                this.menuDigimonImportMinWeight,
                this.menuDigimonUpdateSprites});
            this.menuDigimonDb.Name = "menuDigimonDb";
            this.menuDigimonDb.Size = new System.Drawing.Size(115, 20);
            this.menuDigimonDb.Text = "Digimon Database";
            //
            // menuDigimonRecordMatch
            //
            this.menuDigimonRecordMatch.Name = "menuDigimonRecordMatch";
            this.menuDigimonRecordMatch.Size = new System.Drawing.Size(210, 22);
            this.menuDigimonRecordMatch.Text = "Record Match";
            this.menuDigimonRecordMatch.Click += new System.EventHandler(this.digimonRecordMatch_Click);
            //
            // menuDigimonValidateNames
            //
            this.menuDigimonValidateNames.Name = "menuDigimonValidateNames";
            this.menuDigimonValidateNames.Size = new System.Drawing.Size(210, 22);
            this.menuDigimonValidateNames.Text = "Validate Digimon Names";
            this.menuDigimonValidateNames.Click += new System.EventHandler(this.digimonValidateNames_Click);
            //
            // menuDigimonNormalizeNames
            //
            this.menuDigimonNormalizeNames.Name = "menuDigimonNormalizeNames";
            this.menuDigimonNormalizeNames.Size = new System.Drawing.Size(210, 22);
            this.menuDigimonNormalizeNames.Text = "Normalize Digimon Names";
            this.menuDigimonNormalizeNames.Click += new System.EventHandler(this.digimonNormalizeNames_Click);
            //
            // menuDigimonImportMinWeight
            //
            this.menuDigimonImportMinWeight.Name = "menuDigimonImportMinWeight";
            this.menuDigimonImportMinWeight.Size = new System.Drawing.Size(210, 22);
            this.menuDigimonImportMinWeight.Text = "Import Min Weight";
            this.menuDigimonImportMinWeight.Click += new System.EventHandler(this.digimonImportMinWeight_Click);
            //
            // menuDigimonUpdateSprites
            //
            this.menuDigimonUpdateSprites.Name = "menuDigimonUpdateSprites";
            this.menuDigimonUpdateSprites.Size = new System.Drawing.Size(210, 22);
            this.menuDigimonUpdateSprites.Text = "Update Local Sprite Database";
            this.menuDigimonUpdateSprites.Click += new System.EventHandler(this.digimonUpdateSprites_Click);
            //
            // tabControlMain
            //
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Location = new System.Drawing.Point(0, 24);
            this.tabControlMain.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(1125, 739);
            this.tabControlMain.TabIndex = 1;
            //
            // panelBottom
            //
            this.panelBottom.Controls.Add(this.buttonSave);
            this.panelBottom.Controls.Add(this.buttonCancel);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 763);
            this.panelBottom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1125, 49);
            this.panelBottom.TabIndex = 2;
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
            this.Controls.Add(this.menuStripMain);
            this.MainMenuStrip = this.menuStripMain;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "ModuleEditorForm";
            this.Text = "Module Editor";
            this.menuStripMain.ResumeLayout(false);
            this.menuStripMain.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
