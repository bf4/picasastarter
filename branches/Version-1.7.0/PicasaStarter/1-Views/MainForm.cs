﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;                       // Added to be able to check if a directory exists
using BackupNS;
using HelperClasses;                    
using HelperClasses.Logger;            // Static logging class

namespace PicasaStarter
{
    public partial class MainForm : Form
    {
        #region Private members

        private Settings _settings;
        private string _appDataDir = "";
        private string _appSettingsDir = "";
        private bool _firstRun = false;
        private Backup _backup = null;
        private BackupProgressForm _progressForm = null;
        private int lastSelectedIndexListBoxPicasaDBs = -1;

        #endregion

        #region Public or internal methods

        public MainForm(Settings settings, string appDataDir, string appSettingsDir, bool firstRun)
        {
            InitializeComponent();
            _settings = settings;
            _appDataDir = appDataDir;
            _appSettingsDir = appSettingsDir;
            _firstRun = firstRun;

            ReFillPicasaButtonList();
        }

        internal void CancelBackup()
        {
            if(_backup != null)
                _backup.CancelBackupAssync();
        }

        #endregion

        #region Initialisation and closing of the Form...

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Set version + build time in title bar
            this.Text = this.Text + " " + System.Diagnostics.FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion
                + "   (Build of " + File.GetLastWriteTimeUtc(Application.ExecutablePath).ToString("u", System.Globalization.DateTimeFormatInfo.InvariantInfo) + ")";

            //Ask for apps dir and exe path if new instance
            if (_firstRun == true)
            {
                FirstRunWizard firstRunWizard = new FirstRunWizard(_appSettingsDir, _settings);
                DialogResult result = firstRunWizard.ShowDialog();

                if (result == DialogResult.OK)
                {
                    if (firstRunWizard.ReturnPicasaSettings != null)
                    {
                        _settings = firstRunWizard.ReturnPicasaSettings;
                        _appSettingsDir = firstRunWizard.ReturnAppSettingsDir;
                    }
                    _settings.PicasaExePath = firstRunWizard.ReturnPicasaExePath;

                }
            }

            // Initialise all controls on the screen with the proper data
            ReFillPicasaDBList(false);

            // If the saved defaultselectedDB is valid, select it in the list...
            int defaultSelectedDBIndex = listBoxPicasaDBs.FindStringExact(_settings.picasaDefaultSelectedDB);
            if (defaultSelectedDBIndex != ListBox.NoMatches)
                listBoxPicasaDBs.SelectedIndex = defaultSelectedDBIndex;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if ((listBoxPicasaDBs.SelectedIndex > -1)
                    && listBoxPicasaDBs.SelectedIndex < _settings.picasaDBs.Count)
            {
                _settings.picasaDefaultSelectedDB = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].Name;
            }

            // Save settings
            try
            {
                SettingsHelper.SerializeSettings(_settings,
                        _appSettingsDir + "\\" + SettingsHelper.SettingsFileName);
            }
            catch (Exception ex)
            {
                string message = "Error saving settings: " + ex.Message +
                "\n\nThe Settings directory was not writable or it was on a NAS or Portable Drive that was disconnected." +
                "        ---->   PicasaStarter will Exit.   <----" +
                "\n\nMake sure the NAS or Portable drive is available and try again." +
                "\nGo to General Settings if you wish to select a new settings directory,";

                string caption = "Can't Save Settings File";

                // Displays the MessageBox.
                MessageBox.Show(message, caption);
            }
        }

        #endregion

        #region Event handlers for buttons at the bottom of the main form

        private void buttonGeneralSettings_Click(object sender, EventArgs e)
        {
            GeneralSettingsDialog generalSettingsDialog = new GeneralSettingsDialog(_appSettingsDir, _settings.PicasaExePath);

            DialogResult result = generalSettingsDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                if (generalSettingsDialog.ReturnPicasaSettings != null)
                {
                    _settings = generalSettingsDialog.ReturnPicasaSettings;
                    // ReFillPicasaDBList(false);
                }
                _settings.PicasaExePath = generalSettingsDialog.ReturnPicasaExePath;
                _appSettingsDir = generalSettingsDialog.ReturnAppSettingsDir;
                // Initialise all controls on the screen with the proper data
                ReFillPicasaDBList(false);
                // If the saved defaultselectedDB is valid, select it in the list...
                int defaultSelectedDBIndex = listBoxPicasaDBs.FindStringExact(_settings.picasaDefaultSelectedDB);
                if (defaultSelectedDBIndex != ListBox.NoMatches)
                    listBoxPicasaDBs.SelectedIndex = defaultSelectedDBIndex;

                if (_firstRun == true)
                {
                    ShowHelp();
                }

            }
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            ShowHelp();
        }

        private void ShowHelp()
        {
            HelpDialog help = new HelpDialog();
            help.ShowDialog();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

        #region Tab PicasaDatabases

        private void listBoxPicasaDBs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxPicasaDBs.SelectedIndex < 0)
                return;
            if (listBoxPicasaDBs.SelectedIndex >= _settings.picasaDBs.Count)
            {
                MessageBox.Show("Invalid item choosen from the database list");
                return;
            }
        }

        private void OnListBoxMouseMove(object sender, MouseEventArgs e)
        {
            // Get the item
            int index = listBoxPicasaDBs.IndexFromPoint(e.Location);

            if (index == lastSelectedIndexListBoxPicasaDBs)
                return;
            else
                lastSelectedIndexListBoxPicasaDBs = index;

            string toolTipText = "";
            if ((index >= 0) && (index < listBoxPicasaDBs.Items.Count))
                toolTipText = _settings.picasaDBs[index].Description;

            // Limit the length of the text by adding newlines, otherwise doesn't look good.
            toolTipText = StringHelper.DivideInLines(toolTipText, 100);
            toolTip.SetToolTip(listBoxPicasaDBs, toolTipText);
        }

        private void buttonAddDB_Click(object sender, EventArgs e)
        {
            CreatePicasaDBForm createPicasaDB = new CreatePicasaDBForm();

            DialogResult result = createPicasaDB.ShowDialog();

            // If OK, add the picasaDB as defined in the createPicasaDBForm...
            if (result == DialogResult.OK)
            {
                _settings.picasaDBs.Add(createPicasaDB.PicasaDB);
                ReFillPicasaDBList(true);
            }
        }

        private void buttonEditDB_Click(object sender, EventArgs e)
        {
            bool isStandardDatabase = false;

            if (listBoxPicasaDBs.SelectedIndex == -1
                    || listBoxPicasaDBs.SelectedIndex >= _settings.picasaDBs.Count)
            {
                MessageBox.Show("Please choose a picasa database from the list first");
                return;
            }
            if (_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].IsStandardDB == true)
                isStandardDatabase = true;

            PicasaDB picasaDB = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex];
            CreatePicasaDBForm createPicasaDB = new CreatePicasaDBForm(picasaDB, isStandardDatabase);

            DialogResult result = createPicasaDB.ShowDialog();

            // If OK, update the picasaDB to the edited version
            if (result == DialogResult.OK)
            {
                _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex] = createPicasaDB.PicasaDB;
                ReFillPicasaDBList(true);
            }
        }

        private void buttonRemoveDB_Click(object sender, EventArgs e)
        {
            if (listBoxPicasaDBs.SelectedIndex == -1
                    || listBoxPicasaDBs.SelectedIndex >= _settings.picasaDBs.Count)
            {
                MessageBox.Show("Please choose a picasa database from the list first");
                return;
            }
            if (_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].IsStandardDB == true)
            {
                MessageBox.Show("The default database Picasa creates for you in you user directory cannot be removed...");
                return;
            }

            DialogResult result = MessageBox.Show("Remark: This won't delete the picasa database itself, it will only remove the entry from this list!!!\n\n"
                    + "If you also want to recuperate the (little) diskspace taken by the database, it is better to do this first.\n\n"
                    + "Click \"OK\" if you want to remove the entry from the list, \"Cancel\" to... cancel",
                "Do you want to do this?", MessageBoxButtons.OKCancel);
            if (result == DialogResult.OK)
            {
                _settings.picasaDBs.RemoveAt(listBoxPicasaDBs.SelectedIndex);
                ReFillPicasaDBList(false);
            }
        }

        private void buttonRunPicasa_Click(object sender, EventArgs e)
        {
            if (listBoxPicasaDBs.SelectedIndex == -1)
            {
                MessageBox.Show("Please choose a picasa database from the list first");
                return;
            }
            if (!Directory.Exists(_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BaseDir))
            {
                MessageBox.Show("The base directory of this database doesn't exist or you didn't choose one yet.");
                return;
            }
            WindowState = FormWindowState.Minimized; //Remove PicasaStarter window from desktop while Picasa is running
            
            PicasaRunner runner = new PicasaRunner(_appDataDir, _settings.PicasaExePath);

            // If the user wants to run his personal default database... 
            String dbBaseDir;
            string destButtonDir;

            if (_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].IsStandardDB == true)
            {
                // For using the standard database, the BaseDir to pass needs to be null...
                dbBaseDir = null;

                // Set the directory to put the PicasaButtons in the PicasaDB...
                destButtonDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) +
                        "\\Google\\Picasa2\\buttons";
            }
            // If the user wants to run a custom database...
            else
            {
                // Set the choosen BaseDir
                dbBaseDir = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BaseDir;

                // Set the directory to put the PicasaButtons in the PicasaDB...
                destButtonDir = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BaseDir +
                        "\\Local Settings\\Application Data\\Google\\Picasa2\\buttons";
            }

            string sourceButtonDir = _appSettingsDir + '\\' + SettingsHelper.PicasaButtons;

            // Copy Buttons and scripts and set the correct Path variable to be able to start scripts...
            IOHelper.TryDeleteFiles(destButtonDir, "PS_Button*");
            foreach (PicasaButton button in _settings.picasaButtons.ButtonList)
            {
                button.CreateButtonFile(destButtonDir);
            }
            string path = Environment.GetEnvironmentVariable("PATH");
            path = destButtonDir + "\\;" + path;
            Environment.SetEnvironmentVariable("PATH", path);
            
            _settings.picasaButtons.Registerbuttons();
            
            // Go!
            runner.RunPicasa(dbBaseDir, _appSettingsDir);
            
            // Restore the MainForm...
            WindowState = FormWindowState.Normal;

            // Does the user want a backup?
            DialogResult result = MessageBox.Show("Do you wan't to take a backup of the latest version of your images and database?",
                    "Backup?", MessageBoxButtons.YesNo);
            if(result == DialogResult.Yes)
                StartBackup();
        }

        private void buttonBackupPics_Click(object sender, EventArgs e)
        {
            StartBackup();
        }

        private void BackupCompleted(object sender, EventArgs e)
        {
            this.Enabled = true;
            _progressForm.Hide();
            _progressForm = null;
            _backup = null;
        }

        private void buttonViewBackups_Click(object sender, EventArgs e)
        {
            if (listBoxPicasaDBs.SelectedIndex == -1)
            {
                MessageBox.Show("Please choose a picasa database from the list first");
                return;
            }

            string backupDir = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BackupDir;
            if (!Directory.Exists(backupDir))
            {
                MessageBox.Show("The backup directory of this database doesn't exist or you didn't choose one yet.");
                return;
            }

            try
            {
                Directory.CreateDirectory(backupDir);
                System.Diagnostics.Process.Start(backupDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + ", when trying to open directory: " + backupDir);
            }
        }

        private void StartBackup()
        {
            if (listBoxPicasaDBs.SelectedIndex == -1)
            {
                MessageBox.Show("Please choose a picasa database from the list first");
                return;
            }
            if (!Directory.Exists(_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BaseDir))
            {
                MessageBox.Show("The base directory of this database doesn't exist or you didn't choose one yet.");
                return;
            }
            if (!Directory.Exists(_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BackupDir))
            {
                MessageBox.Show("The backup directory of this database doesn't exist or you didn't choose one yet.");
                return;
            }
            if (_backup != null)
            {
                MessageBox.Show("There is a backup still running... please wait until it is finished before starting one again.");
                return;
            }
            
            try
            {
                // Initialise the paths where the database and the albums can be found
                String picasaDBPath = SettingsHelper.GetFullDBDirectory(_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex]) + "\\Picasa2";
                String picasaAlbumsPath = SettingsHelper.GetFullDBDirectory(_settings.picasaDBs[listBoxPicasaDBs.SelectedIndex]) + "\\Picasa2Albums";

                // Read directories watched/excluded by Picasa in the text files in the Album dir... 
                string watched = File.ReadAllText(picasaAlbumsPath + "\\watchedfolders.txt");
                string excluded = File.ReadAllText(picasaAlbumsPath + "\\frexcludefolders.txt");

                string[] watchedDirs = watched.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                string[] excludedDirs = excluded.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                _backup = new Backup();
                _backup.DestinationDir = _settings.picasaDBs[listBoxPicasaDBs.SelectedIndex].BackupDir;
                _backup.DirsToBackup.AddRange(watchedDirs);     // Backup watched dirs
                _backup.DirsToBackup.Add(picasaDBPath);         // Backup Picasa database
                _backup.DirsToBackup.Add(picasaAlbumsPath);     // Backup albums
                _backup.DirsToExclude.AddRange(excludedDirs);   // Exclude explicitly unwatched dirs
                _backup.MaxNbBackups = 100;                     // Max nb. backups to keep

                _progressForm = new BackupProgressForm(this);
                _progressForm.Show();
                this.Enabled = false;

                _backup.ProgressEvent += new Backup.BackupProgressEventHandler(_progressForm.Progress);
                _backup.CompletedEvent += new Backup.BackupCompletedEventHandler(BackupCompleted);

                // Start the asynchronous operation.
                _backup.StartBackupAssync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ReFillPicasaDBList(bool selectLastItem = false)
        {
            listBoxPicasaDBs.BeginUpdate();
            listBoxPicasaDBs.SelectedIndex = -1;
            listBoxPicasaDBs.Items.Clear();
            
            for (int i = 0; i < _settings.picasaDBs.Count; i++)
            {
                listBoxPicasaDBs.Items.Add(_settings.picasaDBs[i].Name);
            }

            if (listBoxPicasaDBs.Items.Count > 0)
            {
                if (selectLastItem == true)
                    listBoxPicasaDBs.SelectedIndex = listBoxPicasaDBs.Items.Count - 1;
                else
                    listBoxPicasaDBs.SelectedIndex = 0;
            }
            listBoxPicasaDBs.EndUpdate();
        }

        #endregion        

        #region Tab PicasaButtons

        private void buttonAddPicasaButton_Click(object sender, EventArgs e)
        {
            CreatePicasaButtonForm createPicasaButtonForm = new CreatePicasaButtonForm(_appSettingsDir);

            createPicasaButtonForm.ShowDialog();

            if (createPicasaButtonForm.DialogResult == DialogResult.OK)
            {
                _settings.picasaButtons.ButtonList.Add(createPicasaButtonForm.PicasaButton);
                this.ReFillPicasaButtonList();
            }
        }

        private void buttonEditPicasaButton_Click(object sender, EventArgs e)
        {
            if (listBoxPicasaButtons.SelectedIndex == -1
                    || listBoxPicasaButtons.SelectedIndex >= _settings.picasaButtons.ButtonList.Count)
            {
                MessageBox.Show("Please choose a picasa button from the list first");
                return;
            }

            PicasaButton curButton = _settings.picasaButtons.ButtonList[listBoxPicasaButtons.SelectedIndex];
            CreatePicasaButtonForm createPicasaButtonForm = new CreatePicasaButtonForm(curButton, _appSettingsDir);

            createPicasaButtonForm.ShowDialog();

            if (createPicasaButtonForm.DialogResult == DialogResult.OK)
            {
                _settings.picasaButtons.ButtonList[listBoxPicasaButtons.SelectedIndex] = createPicasaButtonForm.PicasaButton;
                this.ReFillPicasaButtonList();
            }
        }

        private void buttonRemovePicasaButton_Click(object sender, EventArgs e)
        {
            if (listBoxPicasaButtons.SelectedIndex == -1
                    || listBoxPicasaButtons.SelectedIndex >= _settings.picasaButtons.ButtonList.Count)
            {
                MessageBox.Show("Please choose a picasa button from the list first");
                return;
            }

            _settings.picasaButtons.ButtonList.RemoveAt(listBoxPicasaButtons.SelectedIndex);
            this.ReFillPicasaButtonList();
        }

        private void listBoxPicasaButtons_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxPicasaButtons.SelectedIndex < 0)
            {
                textBoxPicasaButtonDesc.Text = "";
                return;
            }

            if (listBoxPicasaButtons.SelectedIndex >= _settings.picasaButtons.ButtonList.Count)
            {
                MessageBox.Show("Invalid item choosen from the list");
                return;
            }

            textBoxPicasaButtonDesc.Text = _settings.picasaButtons.ButtonList[listBoxPicasaButtons.SelectedIndex].Description;
        }

        private void ReFillPicasaButtonList()
        {
            listBoxPicasaButtons.BeginUpdate();
            listBoxPicasaButtons.SelectedIndex = -1;
            listBoxPicasaButtons.Items.Clear();

            for (int i = 0; i < _settings.picasaButtons.ButtonList.Count; i++)
            {
                listBoxPicasaButtons.Items.Add(_settings.picasaButtons.ButtonList[i].Label);
            }

            if (listBoxPicasaButtons.Items.Count > 0)
                listBoxPicasaButtons.SelectedIndex = 0;

            listBoxPicasaButtons.EndUpdate();
        }

       #endregion
    }
}
