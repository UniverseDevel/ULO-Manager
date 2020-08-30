using AForge.Video;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ULOControls;
using static ULOController.Controller;

namespace ULOController
{
    public partial class ControllerGUI : Form
    {
        ULO ulo = new ULO();
        MJPEGStream stream = null;
        bool init = false;
        bool stream_running = false;

        private delegate void SafeCallDelegate(string text);

        public ControllerGUI()
        {
            init = true;
            InitializeComponent();
            
            tbUsage.Text = usage();
            
            cbAction.Items.Add(Actions.CallAPI);
            cbAction.Items.Add(Actions.CheckAvailability);
            cbAction.Items.Add(Actions.CleanDiskSpace);
            cbAction.Items.Add(Actions.CurrentSnapshot);
            cbAction.Items.Add(Actions.DownloadLog);
            cbAction.Items.Add(Actions.DownloadSnapshots);
            cbAction.Items.Add(Actions.DownloadVideos);
            cbAction.Items.Add(Actions.GetBattery);
            cbAction.Items.Add(Actions.GetCardSpace);
            cbAction.Items.Add(Actions.GetDiskSpace);
            cbAction.Items.Add(Actions.GetMode);
            cbAction.Items.Add(Actions.IsCard);
            cbAction.Items.Add(Actions.IsPowered);
            cbAction.Items.Add(Actions.MoveToCard);
            cbAction.Items.Add(Actions.SetMode);
            cbAction.Items.Add(Actions.TestAvailability);
            cbAction.SelectedIndex = 0;

            tbHost.Text = Properties.Settings.Default.host;
            tbUsername.Text = Properties.Settings.Default.username;
            tbPassword.Text = Properties.Settings.Default.password;

            setCfgValues();

            tabControl1.TabPages.RemoveByKey("tabPage2"); // TODO remove when/if Live Feed is implemented
            init = false;
        }

        private void setCfgValues()
        {
            cfg_writeLog.Checked = ulo.configuration.writeLog;
            cfg_showArguments.Checked = ulo.configuration.showArguments;
            cfg_showTrace.Checked = ulo.configuration.showTrace;
            cfg_showSkipped.Checked = ulo.configuration.showSkipped;
            cfg_showPingResults.Checked = ulo.configuration.showPingResults;
            cfg_suppressLogHandling.Checked = ulo.configuration.suppressLogHandling;
        }

        private void startStream()
        {
            stream.Start();
            stream_running = true;
        }

        private void stopStream()
        {
            stream.Stop();
            stream_running = false;
        }

        private void stream_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bmp = (Bitmap)eventArgs.Frame.Clone();
            pbLiveFeed.Image = bmp;
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            if (!stream_running)
            {
                try
                {
                    stream = new MJPEGStream("ws://" + tbHost.Text + "/api/v1/live");
                    stream.Login = tbUsername.Text;
                    stream.Password = tbPassword.Text;
                    stream.NewFrame += stream_NewFrame;
                    startStream();
                }
                catch (Exception ex)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: " + ex);
                }
            }

            if (tbHost.Text != String.Empty && tbUsername.Text != String.Empty && tbPassword.Text != String.Empty && cbAction.Items[cbAction.SelectedIndex].ToString() != String.Empty)
            {
                Properties.Settings.Default.host = tbHost.Text;
                Properties.Settings.Default.username = tbUsername.Text;
                Properties.Settings.Default.password = tbPassword.Text;
                Properties.Settings.Default.Save();

                btnExecute.Enabled = false;
                tbOutput.Text = String.Empty;
                try
                {
                    string execCmd = "\"" + tbHost.Text + "\" \"" + tbUsername.Text + "\" \"" + tbPassword.Text + "\" \"" + cbAction.Items[cbAction.SelectedIndex].ToString() + "\" \"" + tbArg1.Text + "\" \"" + tbArg2.Text + "\" \"" + tbArg3.Text + "\" \"" + tbArg4.Text + "\" \"" + tbArg5.Text + "\" \"" + tbArg6.Text + "\" \"" + tbArg7.Text + "\" \"" + tbArg8.Text + "\"";
                    Process process = new Process();
                    process.StartInfo.FileName = product_location + @"\" + product_filename + ".exe";
                    process.StartInfo.Arguments = execCmd;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.OutputDataReceived += text_Received;
                    process.ErrorDataReceived += text_Received;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (processExists(process.Id))
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: " + ex);
                }
                btnExecute.Enabled = true;
            }
            else
            {
                if (tbHost.Text == String.Empty)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Hostname not provided.");
                }
                if (tbUsername.Text == String.Empty)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Username not provided.");
                }
                if (tbPassword.Text == String.Empty)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Password not provided.");
                }
                if (cbAction.Items[cbAction.SelectedIndex].ToString() == String.Empty)
                {
                    writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: Action not provided.");
                }
            }
        }

        private void text_Received(object sender, DataReceivedEventArgs e)
        {
            writeOutput(e.Data);
        }

        private void writeOutput(string text)
        {
            if (tbOutput.InvokeRequired)
            {
                var call = new SafeCallDelegate(writeOutput);
                tbOutput.Invoke(call, new object[] { text });
            }
            else
            {
                tbOutput.Text += text + Environment.NewLine;
                tbOutput.SelectionStart = tbOutput.Text.Length;
                tbOutput.SelectionLength = 0;
            }
        }

        private bool processExists(int pid)
        {
            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == pid)
                {
                    return true;
                }
            }
            return false;
        }

        private void cfgValue_CheckedChanged(object sender, EventArgs e)
        {
            if (!init)
            {
                CheckBox checkbox = (CheckBox)sender;
                string cfgName = checkbox.Name.Replace("cfg_", String.Empty);
                bool cfgValue = checkbox.Checked;
                
                foreach (FieldInfo fieldInfo in ulo.configuration.GetType().GetFields())
                {
                    if (fieldInfo.Name == cfgName)
                    {
                        fieldInfo.SetValue(ulo.configuration, cfgValue);
                    }
                }

                ulo.writeConfig();
                ulo.reloadConfiguration();
                setCfgValues();
            }
        }

        private void ControllerGUI_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (stream_running)
            {
                stopStream();
                pbLiveFeed.Image = null;
            }
            ulo.handleTempLogs();
            Application.Exit();
        }
    }
}
