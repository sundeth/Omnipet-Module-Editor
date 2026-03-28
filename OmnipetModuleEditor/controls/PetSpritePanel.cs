using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace OmnipetModuleEditor.Controls
{
    /// <summary>
    /// Panel that displays pet sprites and action buttons (Download, Import, Refresh).
    /// Handles sprite display and related actions for a pet.
    /// </summary>
    public class PetSpritePanel : UserControl
    {
        // Sprite display boxes
        private readonly List<PictureBox> spriteBoxes = new List<PictureBox>();
        private PictureBox portraitBox;

        // Action buttons
        private readonly Button btnDownload;
        private readonly Button btnImport;
        private readonly Button btnRefresh;

        /// <summary>
        /// The current pet whose sprites are being displayed.
        /// </summary>
        public Pet CurrentPet { get; set; }

        /// <summary>
        /// The current module context.
        /// </summary>
        public Module CurrentModule { get; set; }

        /// <summary>
        /// The path to the module directory.
        /// </summary>
        public string ModulePath { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PetSpritePanel"/> class.
        /// </summary>
        public PetSpritePanel()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 10,
                RowCount = 4,
                AutoSize = true
            };

            // Set up columns (8 sprite columns + 2 for double-width special + 1 button column)
            for (int i = 0; i < 10; i++)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));

            // Set up rows (alternating sprite rows at 48px and label rows at 20px)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F)); // Row 0: First sprite row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F)); // Row 1: First label row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F)); // Row 2: Second sprite row
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F)); // Row 3: Second label row

            string[] spriteLabels = new[]
            {
                "IDLE1", "IDLE2", "HAPPY", "ANGRY",
                "TRAIN1", "TRAIN2", "ATK1", "ATK2",
                "EAT1", "EAT2", "NOPE", "NAP1",
                "NAP2", "SICK", "LOSE"
            };

            // First row: 8 sprites (IDLE1 through ATK2)
            for (int i = 0; i < 8; i++)
            {
                var pb = CreateSpriteBox();
                layout.Controls.Add(pb, i, 0);
                spriteBoxes.Add(pb);

                var lbl = CreateSpriteLabel(spriteLabels[i]);
                layout.Controls.Add(lbl, i, 1);
            }

            // Add Download button after ATK2 (column 8, row 0)
            btnDownload = new Button
            {
                Text = Properties.Resources.Button_Download ?? "Download",
                Width = 48,
                Height = 48,
                Margin = new Padding(2),
                Font = new Font(FontFamily.GenericSansSerif, 6.5f, FontStyle.Regular)
            };
            layout.Controls.Add(btnDownload, 8, 0);

            // Add Import button after Download (column 9, row 0)
            btnImport = new Button
            {
                Text = Properties.Resources.Button_Import ?? "Import",
                Width = 48,
                Height = 48,
                Margin = new Padding(2),
                Font = new Font(FontFamily.GenericSansSerif, 6.5f, FontStyle.Regular)
            };
            layout.Controls.Add(btnImport, 9, 0);

            // Second row: 7 sprites (EAT1 through LOSE)
            for (int i = 0; i < 7; i++)
            {
                var pb = CreateSpriteBox();
                layout.Controls.Add(pb, i, 2);
                spriteBoxes.Add(pb);

                var lbl = CreateSpriteLabel(spriteLabels[8 + i]);
                layout.Controls.Add(lbl, i, 3);
            }

            // Add PORTRAIT/SPECIAL box (double width, spanning columns 7-8)
            portraitBox = new PictureBox
            {
                Width = 104, // Double width (48*2 + margin)
                Height = 48,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(2),
                Cursor = Cursors.Hand
            };
            portraitBox.Click += PortraitBox_Click;
            layout.Controls.Add(portraitBox, 7, 2);
            layout.SetColumnSpan(portraitBox, 2);

            var portraitLabel = new Label
            {
                Text = "SPECIAL",
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Width = 104,
                Height = 16,
                Font = new Font(FontFamily.GenericSansSerif, 7, FontStyle.Regular)
            };
            layout.Controls.Add(portraitLabel, 7, 3);
            layout.SetColumnSpan(portraitLabel, 2);

            // Add Refresh button after SPECIAL (column 9, row 2)
            btnRefresh = new Button
            {
                Text = Properties.Resources.Button_Refresh ?? "Refresh",
                Width = 48,
                Height = 48,
                Margin = new Padding(2),
                Font = new Font(FontFamily.GenericSansSerif, 6.5f, FontStyle.Regular)
            };
            layout.Controls.Add(btnRefresh, 9, 2);

            this.Controls.Add(layout);

            // Button event handlers
            btnDownload.Click += BtnDownload_Click;
            btnImport.Click += BtnImport_Click;
            btnRefresh.Click += BtnRefresh_Click;
        }

        #region Sprite Display

        /// <summary>
        /// Sets the images for the sprite boxes.
        /// </summary>
        /// <param name="sprites">List of images to display.</param>
        public void SetSprites(List<Image> sprites)
        {
            for (int i = 0; i < spriteBoxes.Count; i++)
            {
                spriteBoxes[i].Image = (sprites != null && i < sprites.Count) ? sprites[i] : null;
            }

            // Set portrait image (frame 15)
            if (sprites != null && sprites.Count > 15)
            {
                portraitBox.Image = sprites[15];
            }
            else
            {
                portraitBox.Image = null;
            }
        }

        /// <summary>
        /// Loads and displays the sprites for the current pet.
        /// </summary>
        public void RefreshSprites()
        {
            if (CurrentPet != null && CurrentModule != null && !string.IsNullOrEmpty(ModulePath))
            {
                var sprites = PetUtils.LoadPetSprites(ModulePath, CurrentModule, CurrentPet, 16); // Load 16 to include portrait
                SetSprites(sprites);
            }
            else
            {
                SetSprites(null);
            }
        }

        #endregion

        #region Button Event Handlers

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            if (CurrentPet != null && !string.IsNullOrWhiteSpace(CurrentPet.Name))
            {
                try
                {
                    Clipboard.SetText(CurrentPet.Name);
                }
                catch
                {
                    MessageBox.Show(Properties.Resources.CouldNotCopyName ?? "Could not copy the name to the clipboard.",
                        Properties.Resources.Error ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://dmc-sprite-database.vercel.app/",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(Properties.Resources.CouldNotOpenBrowser ?? "Could not open the browser.",
                    Properties.Resources.Error ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            if (CurrentPet == null || CurrentModule == null || string.IsNullOrWhiteSpace(CurrentPet.Name) || string.IsNullOrWhiteSpace(ModulePath))
            {
                MessageBox.Show(Properties.Resources.ImportSpritesSelectPet ?? "Select a valid pet to import sprites.",
                    Properties.Resources.Warning ?? "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Use fixed name format instead of module.NameFormat
            string zipName = SpriteUtils.GetSpriteName(CurrentPet.Name, SpriteUtils.DefaultNameFormat) + ".zip";

            string downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloadsFolder = Path.Combine(downloads, "Downloads");
            string zipPath = Path.Combine(downloadsFolder, zipName);

            if (!File.Exists(zipPath))
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.ImportSpritesFileNotFound ?? "File not found: {0}", zipPath),
                    Properties.Resources.ImportSpritesTitle ?? "Import sprites",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                string.Format(Properties.Resources.ImportSpritesConfirm ?? "Import sprites from \"{0}\" for this pet?\n\nThe zip file will be copied to the monsters folder.", zipName),
                Properties.Resources.ImportSpritesTitle ?? "Import sprites",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Determine which monsters folder to use based on primary sprite format
            string primaryFormat = SpriteUtils.NormalizeFormat(CurrentModule?.PrimarySpriteFormat);
            string monstersFolder = SpriteUtils.GetFolderForFormat(primaryFormat);
            string targetFolder = Path.Combine(ModulePath, monstersFolder);
            
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            // Copy the zip file to the appropriate monsters folder
            string destinationZipPath = Path.Combine(targetFolder, zipName);

            try
            {
                File.Copy(zipPath, destinationZipPath, true);

                MessageBox.Show(Properties.Resources.ImportSpritesSuccess ?? "Sprites imported successfully!",
                    Properties.Resources.ImportSpritesTitle ?? "Import sprites", MessageBoxButtons.OK, MessageBoxIcon.Information);

                RefreshSprites();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.ImportSpritesError ?? "Error importing sprites: {0}", ex.Message),
                    Properties.Resources.Error ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshSprites();
        }

        private void PortraitBox_Click(object sender, EventArgs e)
        {
            if (CurrentPet == null || CurrentModule == null || string.IsNullOrWhiteSpace(CurrentPet.Name) || string.IsNullOrWhiteSpace(ModulePath))
            {
                MessageBox.Show("Please select a valid pet before adding a portrait.",
                    "No Pet Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "PNG Images|*.png";
                openFileDialog.Title = "Select Portrait Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Load the selected image
                        Image portraitImage = Image.FromFile(openFileDialog.FileName);

                        // Find where the sprites are actually stored using the format-aware fallback logic
                        string primaryFormat = SpriteUtils.NormalizeFormat(CurrentModule?.PrimarySpriteFormat);
                        string secondaryFormat = SpriteUtils.NormalizeFormat(CurrentModule?.SecondarySpriteFormat);
                        string spriteName = SpriteUtils.GetSpriteName(CurrentPet.Name, SpriteUtils.DefaultNameFormat);

                        var locationResult = SpriteUtils.FindSpriteLocation(CurrentPet.Name, ModulePath, SpriteUtils.DefaultNameFormat, primaryFormat, secondaryFormat);
                        string spriteLocation = locationResult.LoadedPath;

                        if (spriteLocation == null)
                        {
                            MessageBox.Show("Could not find existing sprites for this pet. Please import sprites first using the Import button.",
                                "Sprites Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Add portrait to the found location
                        bool success = AddPortraitToLocation(spriteLocation, openFileDialog.FileName);

                        if (success)
                        {
                            // Update the portrait box
                            portraitBox.Image = portraitImage;

                            MessageBox.Show("Portrait added successfully!",
                                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to add portrait to sprite location.",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error adding portrait: {ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Adds a portrait (15.png) to the specified location (either a directory or zip file).
        /// </summary>
        private bool AddPortraitToLocation(string location, string portraitSourcePath)
        {
            try
            {
                if (Directory.Exists(location))
                {
                    // It's a directory - just copy the file
                    string destPath = Path.Combine(location, "15.png");
                    File.Copy(portraitSourcePath, destPath, true);
                    return true;
                }
                else if (File.Exists(location) && location.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // It's a zip file - add/update the portrait inside
                    AddPortraitToZip(location, portraitSourcePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void AddPortraitToZip(string zipPath, string portraitPath)
        {
            // Create a temporary file to store the modified zip
            string tempZipPath = Path.GetTempFileName();

            try
            {
                // Copy existing zip to temp location
                File.Copy(zipPath, tempZipPath, true);

                using (var zip = ZipFile.Open(tempZipPath, ZipArchiveMode.Update))
                {
                    // Remove existing 15.png if it exists
                    var existingEntry = zip.GetEntry("15.png");
                    if (existingEntry != null)
                    {
                        existingEntry.Delete();
                    }

                    // Add new portrait
                    zip.CreateEntryFromFile(portraitPath, "15.png");
                }

                // Replace original zip with modified version
                File.Copy(tempZipPath, zipPath, true);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a PictureBox for displaying a sprite.
        /// </summary>
        private static PictureBox CreateSpriteBox()
        {
            return new PictureBox
            {
                Width = 48,
                Height = 48,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(2)
            };
        }

        /// <summary>
        /// Creates a label for a sprite box.
        /// </summary>
        private static Label CreateSpriteLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.TopCenter,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Width = 48,
                Height = 16,
                Font = new Font(FontFamily.GenericSansSerif, 7, FontStyle.Regular)
            };
        }

        #endregion
    }
}