﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

using BilllingMachine.Data;
using BilllingMachine.Models;
using BilllingMachine.Common;


namespace BilllingMachine.UIForms
{
    public partial class BillingSystemForm : Form
    {
        const int ITERATIONS_NUM_VALUE = 100;

        //const string ROOT_PROJECT_DIR = (@"..\\..\\Resources\\");
        //const string COUNTRY_FILE_NAME = (@"..\\..\\Resources\\country.txt");
        //const string RATES_FILE_NAME = (@"..\\..\\Resources\\rates.csv");

        private List<DataGridView> gridsList = new List<DataGridView>();

        Factory[] Factories = new Factory[3];

        private string callsFileName = Globals.EMPTY_STRING;
        private string callsFilesPath = Globals.EMPTY_STRING;

        private ProgressBarForm frmProgress = null;
        private Stopwatch stopWatch = null;


        public BillingSystemForm()
        {
            InitializeComponent();
            tabCommon.SelectedIndexChanged += new EventHandler(tabCommon_SelectedIndexchanged);
        }

        private void tabCommon_SelectedIndexchanged(object sender, EventArgs e)
        {
            switch ((sender as TabControl).SelectedIndex)
            {
                case 0:
                    btnCountry.Visible = false;
                    btnRates.Visible = false;
                    btnCalls.Visible = false;
                    lblTotalRows.Visible = false;
                    //prgBar.Visible = true;
                    break;
                case 1:
                    btnCountry.Visible = true;
                    btnRates.Visible = false;
                    btnCalls.Visible = false;
                    //prgBar.Visible = false;
                    lblTotalRows.Visible = true;
                    lblTotalRows.Text = this.getTotalCounrtyRows();
                    break;
                case 2:
                    btnCountry.Visible = false;
                    btnRates.Visible = true;
                    btnCalls.Visible = false;
                    //prgBar.Visible = false;
                    lblTotalRows.Visible = true;
                    lblTotalRows.Text = this.getTotalRatesRows();
                    break;
                case 3:
                    btnCountry.Visible = false;
                    btnRates.Visible = false;
                    //prgBar.Visible = false;
                    btnCalls.Visible = true;
                    lblTotalRows.Visible = true;
                    lblTotalRows.Text = this.getTotalCallsRows(callsFileName);
                    break;
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnCountry_Click(object sender, EventArgs e)
        {
            this.gridCountry.DataSource = Factories[0].GetDataSource().LoadData(Globals.COUNTRY_FILE_NAME).Tables[0].DefaultView;
            this.lblTotalRows.Text = this.getTotalCounrtyRows();
        }

        private void btnCalls_Click(object sender, EventArgs e)
        {
            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open Calls TXT File";
            fDialog.Filter = "TXT Files|calls*.txt";
            fDialog.InitialDirectory = (callsFilesPath.Equals(Globals.EMPTY_STRING)) ? Globals.ROOT_PROJECT_DIR : callsFilesPath;
            // fDialog.InitialDirectory = @"C:\";
            fDialog.AddExtension = true;
            fDialog.CheckFileExists = true;
            fDialog.CheckPathExists = true;
            if (fDialog.ShowDialog() == DialogResult.OK)
            {
                callsFilesPath = fDialog.FileName.ToString();
                callsFileName = fDialog.SafeFileName;
                this.gridCalls.DataSource = Factories[2].GetDataSource().LoadData(callsFilesPath).Tables[0].DefaultView;
                this.lblTotalRows.Text = this.getTotalCallsRows(callsFileName);
                this.btnRun.Enabled = true;
            }
        }

        private void btnRates_Click(object sender, EventArgs e)
        {
            this.gridRates.DataSource = Factories[1].GetDataSource().LoadData(Globals.RATES_FILE_NAME).Tables[0].DefaultView;
            this.lblTotalRows.Text = this.getTotalRatesRows();
        }

        #region Synchronous BackgroundWorker Thread

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (checkAllDataLoaded())
            {
                // Create a background thread
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);

                // Create a progress form on the UI thread
                frmProgress = new ProgressBarForm();

                // Kick off the Async thread
                bw.RunWorkerAsync();

                // Lock up the UI with this modal progress form.
                tabCommon.SelectedTab = tabGeneral;
                frmProgress.ShowDialog(this);
                frmProgress = null;
            }
            else
            {
                MessageBox.Show("Check that all data in tabs were successfully loaded!");
            }
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            long calls_num = 0;
            int prgoreesPersentage = 100 * frmProgress.progressBar.Step / ITERATIONS_NUM_VALUE;

            stopWatch = new Stopwatch();
            
            // Seach rates for directions
            ProcessData.ProcessRates();

            // Proccess calls' list (here 100 iterations for one selected 'call.txt' file
            stopWatch.Start();
            for (int i = 1; i <= ITERATIONS_NUM_VALUE; i++)
            {
                Thread.Sleep(100);

                WriteResults.RemoveOutputFile(Globals.OUTUPUT_FILE_NAME);
                
                frmProgress.progressBar.Invoke((MethodInvoker)delegate()
                {
                    this.lblState.Text = "STATUS: PROCCESSING...";
                    frmProgress.progressBar.Value = i;
                    calls_num = calls_num + ProcessData.ProccessCalls();
                    this.lblStatus.Text = "COMPLETED: " + i * prgoreesPersentage + "%";
                    this.lblProccess.Text = "PROCCESSED CALLS: " + calls_num.ToString();
                    this.lblTime.Text = string.Format("{0} {1} {2}", "TOTAL PROCCESS TIME IS:", getTimeSpan(), "ms.");
                });

                if (frmProgress.CanceledProccess)
                {
                    // Set the e.Cancel flag so that the WorkerCompleted event
                    // knows that the process was cancelled.
                    e.Cancel = true;
                    return;
                }
            }
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (frmProgress != null)
            {
                frmProgress.Hide();
                frmProgress = null;
            }

            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
                return;
            }

            // Check to see if the background process was cancelled.
            if (e.Cancelled)
            {
                stopWatch.Stop();
                lblState.Text = "STATUS: Canceled!";
                MessageBox.Show("Processing cancelled.");
                return;
            }

            // Everything completed normally.
            stopWatch.Stop();
            lblState.Text = "STATUS: Completed!";
            MessageBox.Show("Processing is complete.");
        }
        #endregion

        private string getTimeSpan()
        {
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format
            (
                "{0:00}:{1:00}:{2:00}.{3:00}", 
                ts.Hours, 
                ts.Minutes, 
                ts.Seconds, 
                ts.Milliseconds / 10
            );

            return elapsedTime;
        }

        private bool checkAllDataLoaded()
        {
            foreach (DataGridView gridName in gridsList)
            {
                if (gridName.RowCount == 0) 
                    return false;
            }

            return true;
        }

        private string getTotalCounrtyRows()
        {
            return string.Format
            (
                "{0}: {1}", "Total Records", this.gridCountry.RowCount
            );
        }

        private string getTotalRatesRows()
        {
            return string.Format
            (
                "{0}: {1}", "Total Records", this.gridRates.RowCount
            );
        }

        private string getTotalCallsRows(string fileName)
        {
            if (fileName.Equals(Globals.EMPTY_STRING))
            {
                return string.Format
                (
                    "{0}: {1}", "Total Records", this.gridCalls.RowCount
                );
            }
            else
            {
                return string.Format
                (
                    "{0} '{1}': {2}", "Total Records in", fileName, this.gridCalls.RowCount
                );
            }
        }

        private void BillingSystem_Load(object sender, EventArgs e)
        {
            Factories[0] = new FactoryCountry();
            Factories[1] = new FactoryRates();
            Factories[2] = new FactoryCalls();

            gridsList.Add(this.gridCountry);
            gridsList.Add(this.gridRates);
            gridsList.Add(this.gridCalls);
        }
    }
}
