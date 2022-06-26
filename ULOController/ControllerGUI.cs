using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using ULOControls;
using WebSocketSharp;
using static ULOController.Controller;

namespace ULOController
{
    public partial class ControllerGUI : Form
    {
        private long maxFileSize = 100 * (long)Math.Pow(1024, 2); // bytes
        private string storagePath = product_location + "\\media";
        private string videoFile = String.Empty;
        private int fileRetention = 5;
        private WebSocket ws;
        private bool fileReset = false;
        
        private readonly LibVLC _libVlc; // https://code.videolan.org/mfkl/libvlcsharp-samples
        private MemoryStream _memoryStreamVlc;
        private StreamMediaInput _streamMediaInputVlc;
        private Media _mediaVlc;

        private string generate_video_filename()
        {
            return storagePath + "\\video-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
        }

        ULO ulo = new ULO();
        bool init = false;
        bool stream_running = false;

        private delegate void SafeCallDelegate(string text);

        public ControllerGUI()
        {
            init = true;
            InitializeComponent();

            // LibVLC initiation
            Core.Initialize();
            _libVlc = new LibVLC();
            videoView1.MediaPlayer = new MediaPlayer(_libVlc);
            videoView1.MediaPlayer.Volume = 50;
            videoView1.BackgroundImage = null;

            tbUsage.Text = usage();
            
            cbAction.Items.Add(Actions.CallAPI);
            cbAction.Items.Add(Actions.LiveFeed);
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

        private void addLine(string text)
        {
            this.Invoke((MethodInvoker)(() => tbOutput.Select(tbOutput.TextLength + 1, 0)));
            this.Invoke((MethodInvoker)(() => tbOutput.SelectedText = Environment.NewLine + text));
        }

        private void AppendAllBytes(string path, byte[] bytes)
        {
            FileInfo fi = new FileInfo(path);
            if (!Directory.Exists(fi.DirectoryName))
            {
                Directory.CreateDirectory(fi.DirectoryName);
            }

            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            if (fi.Length > maxFileSize)
            {
                videoFile = generate_video_filename();
                addLine("Maximum file size reached, starting new video file.");
                addLine("Video location: " + videoFile);

                fileReset = true;
                ws.Close();
                ws.Connect();
                fileReset = false;

                DirectoryInfo info = new DirectoryInfo(fi.DirectoryName);
                FileInfo[] files = info.GetFiles("video-*.mp4");
                Array.Sort(files, delegate (FileInfo f1, FileInfo f2)
                {
                    return f2.CreationTime.CompareTo(f1.CreationTime);
                });
                int counter = 0;
                foreach (FileInfo file in files)
                {
                    counter++;
                    if (counter > fileRetention)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch { }
                    }
                }
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancelStream();
        }

        private void cancelStream()
        {
            try
            {
                // TODO
            }
            catch (Exception ex) { }
            ws.Close(CloseStatusCode.Normal);
        }

        private void btnExecute_Click(object sender_main, EventArgs e_main)
        {

            if (tbHost.Text != String.Empty && tbUsername.Text != String.Empty && tbPassword.Text != String.Empty && cbAction.Items[cbAction.SelectedIndex].ToString() != String.Empty)
            {
                Properties.Settings.Default.host = tbHost.Text;
                Properties.Settings.Default.username = tbUsername.Text;
                Properties.Settings.Default.password = tbPassword.Text;
                Properties.Settings.Default.Save();

                tbOutput.Text = String.Empty;

                if (cbAction.Items[cbAction.SelectedIndex].ToString() == Actions.LiveFeed)
                {
                    if (!stream_running)
                    {
                        bool record = false;
                        if (tbArg1.Text != String.Empty)
                        {
                            record = Convert.ToBoolean(Convert.ToInt32(tbArg1.Text));
                        }
                        if (tbArg2.Text != String.Empty)
                        {
                            storagePath = tbArg2.Text;
                        }
                        if (tbArg3.Text != String.Empty)
                        {
                            maxFileSize = Convert.ToInt32(tbArg3.Text) * (long)Math.Pow(1024, 2);
                        }
                        if (tbArg4.Text != String.Empty)
                        {
                            fileRetention = Convert.ToInt32(tbArg4.Text);
                        }
                        videoFile = generate_video_filename();

                        try
                        {
                            string[] protocols = new string[] { "mudesign.ulo.mp4" };
                            ws = new WebSocket(new Uri("ws://" + tbHost.Text + "/api/v1/live").AbsoluteUri, protocols);
                            //ws.Log.Level = WebSocketSharp.LogLevel.Trace;
                            //ws.Log.File = ULO.errFile;
                            //ws.SetProxy("http://" + tbHost.Text, tbUsername.Text, tbPassword.Text);
                            //ws.SetCredentials(tbUsername.Text, tbPassword.Text, true);
                            ws.Origin = "http://" + tbHost.Text;
                            ws.EnableRedirection = true;
                            ws.EmitOnPing = true;
                            ws.OnOpen += (sender, e) =>
                            {
                                addLine("Connection opened.");
                                if (record)
                                {
                                    addLine("Video location: " + videoFile);
                                }
                                stream_running = true;
                                if (!fileReset)
                                {
                                    this.Invoke((MethodInvoker)(() =>
                                    {
                                        btnExecute.Enabled = false;
                                        btnCancel.Visible = true;
                                    }));
                                }
                            };
                            ws.OnMessage += (sender, e) =>
                            {
                                if (e.IsText)
                                {
                                    addLine("Text message received: " + e.Data + ".");
                                    return;
                                }
                                if (e.IsBinary)
                                {
                                    addLine("Binary message received of size: " + e.RawData.Length + ".");
                                    if (record)
                                    {
                                        AppendAllBytes(videoFile, e.RawData);
                                    }
                                    /*
                                    https://github.com/jeremyVignelles/libvlcsharp-nonfree-samples/blob/main/
                                    _memoryStreamVlc = new MemoryStream(e.RawData);
                                    _streamMediaInputVlc = new StreamMediaInput(_memoryStreamVlc);
                                    _mediaVlc = new Media(_libVlc, _streamMediaInputVlc);
                                    if (videoView1.MediaPlayer != null) videoView1.MediaPlayer.Play(_mediaVlc);
                                    */
                                    return;
                                }
                                if (e.IsPing)
                                {
                                    //addLine("Ping received.");
                                    return;
                                }
                            };
                            ws.OnError += (sender, e) =>
                            {
                                addLine("ERROR: " + e.Message + ".");
                                throw e.Exception;
                            };
                            ws.OnClose += (sender, e) =>
                            {
                                addLine("Connection closed.");
                                addLine("Reason: " + e.Reason);
                                addLine("WasClean: " + e.WasClean);
                                stream_running = false;
                                if (!fileReset)
                                {
                                    this.Invoke((MethodInvoker)(() =>
                                    {
                                        btnExecute.Enabled = true;
                                        btnCancel.Visible = false;
                                    }));
                                }
                            };

                            ws.Connect();
                        }
                        catch (Exception ex)
                        {
                            writeOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: " + ex);
                        }
                    }
                }
                else
                {
                    btnExecute.Enabled = false;

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
                cancelStream();
            }
            ulo.handleTempLogs();
            Application.Exit();
        }
    }
}