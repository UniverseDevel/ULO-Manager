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
    public partial class ControllerGui : Form
    {
        private long _maxFileSize = 100 * (long)Math.Pow(1024, 2); // bytes
        private string _storagePath = productLocation + "\\media";
        private string _videoFile = String.Empty;
        private int _fileRetention = 5;
        private WebSocket _ws;
        private bool _fileReset = false;
        
        private readonly LibVLC _libVlc; // https://code.videolan.org/mfkl/libvlcsharp-samples
        private MemoryStream _memoryStreamVlc;
        private StreamMediaInput _streamMediaInputVlc;
        private Media _mediaVlc;

        private string generate_video_filename()
        {
            return _storagePath + "\\video-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
        }

        Ulo _ulo = new Ulo();
        bool _init = false;
        bool _streamRunning = false;

        private delegate void SafeCallDelegate(string text);

        public ControllerGui()
        {
            _init = true;
            InitializeComponent();

            // LibVLC initiation
            Core.Initialize();
            _libVlc = new LibVLC();
            videoView1.MediaPlayer = new MediaPlayer(_libVlc);
            videoView1.MediaPlayer.Volume = 50;
            videoView1.BackgroundImage = null;

            tbUsage.Text = Usage();
            
            cbAction.Items.Add(Actions.CallApi);
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

            SetCfgValues();

            _init = false;
        }

        private void SetCfgValues()
        {
            cfg_writeLog.Checked = _ulo.configuration.writeLog;
            cfg_showArguments.Checked = _ulo.configuration.showArguments;
            cfg_showTrace.Checked = _ulo.configuration.showTrace;
            cfg_showSkipped.Checked = _ulo.configuration.showSkipped;
            cfg_showPingResults.Checked = _ulo.configuration.showPingResults;
            cfg_suppressLogHandling.Checked = _ulo.configuration.suppressLogHandling;
        }

        private void AddLine(string text)
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

            if (fi.Length > _maxFileSize)
            {
                _videoFile = generate_video_filename();
                AddLine("Maximum file size reached, starting new video file.");
                AddLine("Video location: " + _videoFile);

                _fileReset = true;
                _ws.Close();
                _ws.Connect();
                _fileReset = false;

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
                    if (counter > _fileRetention)
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
            CancelStream();
        }

        private void CancelStream()
        {
            try
            {
                // TODO
            }
            catch (Exception ex) { }
            _ws.Close(CloseStatusCode.Normal);
        }

        private void btnExecute_Click(object senderMain, EventArgs eMain)
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
                    if (!_streamRunning)
                    {
                        bool record = false;
                        if (tbArg1.Text != String.Empty)
                        {
                            record = Convert.ToBoolean(Convert.ToInt32(tbArg1.Text));
                        }
                        if (tbArg2.Text != String.Empty)
                        {
                            _storagePath = tbArg2.Text;
                        }
                        if (tbArg3.Text != String.Empty)
                        {
                            _maxFileSize = Convert.ToInt32(tbArg3.Text) * (long)Math.Pow(1024, 2);
                        }
                        if (tbArg4.Text != String.Empty)
                        {
                            _fileRetention = Convert.ToInt32(tbArg4.Text);
                        }
                        _videoFile = generate_video_filename();

                        try
                        {
                            string[] protocols = new string[] { "mudesign.ulo.mp4" };
                            _ws = new WebSocket(new Uri("ws://" + tbHost.Text + "/api/v1/live").AbsoluteUri, protocols);
                            //ws.Log.Level = WebSocketSharp.LogLevel.Trace;
                            //ws.Log.File = ULO.errFile;
                            //ws.SetProxy("http://" + tbHost.Text, tbUsername.Text, tbPassword.Text);
                            //ws.SetCredentials(tbUsername.Text, tbPassword.Text, true);
                            _ws.Origin = "http://" + tbHost.Text;
                            _ws.EnableRedirection = true;
                            _ws.EmitOnPing = true;
                            _ws.OnOpen += (sender, e) =>
                            {
                                AddLine("Connection opened.");
                                if (record)
                                {
                                    AddLine("Video location: " + _videoFile);
                                }
                                _streamRunning = true;
                                if (!_fileReset)
                                {
                                    this.Invoke((MethodInvoker)(() =>
                                    {
                                        btnExecute.Enabled = false;
                                        btnCancel.Visible = true;
                                    }));
                                }
                            };
                            _ws.OnMessage += (sender, e) =>
                            {
                                if (e.IsText)
                                {
                                    AddLine("Text message received: " + e.Data + ".");
                                    return;
                                }
                                if (e.IsBinary)
                                {
                                    AddLine("Binary message received of size: " + e.RawData.Length + ".");
                                    if (record)
                                    {
                                        AppendAllBytes(_videoFile, e.RawData);
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
                            _ws.OnError += (sender, e) =>
                            {
                                AddLine("ERROR: " + e.Message + ".");
                                throw e.Exception;
                            };
                            _ws.OnClose += (sender, e) =>
                            {
                                AddLine("Connection closed.");
                                AddLine("Reason: " + e.Reason);
                                AddLine("WasClean: " + e.WasClean);
                                _streamRunning = false;
                                if (!_fileReset)
                                {
                                    this.Invoke((MethodInvoker)(() =>
                                    {
                                        btnExecute.Enabled = true;
                                        btnCancel.Visible = false;
                                    }));
                                }
                            };

                            _ws.Connect();
                        }
                        catch (Exception ex)
                        {
                            WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: " + ex);
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
                        process.StartInfo.FileName = productLocation + @"\" + productFilename + ".exe";
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

                        while (ProcessExists(process.Id))
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: " + ex);
                    }

                    btnExecute.Enabled = true;
                }
            }
            else
            {
                if (tbHost.Text == String.Empty)
                {
                    WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Hostname not provided.");
                }
                if (tbUsername.Text == String.Empty)
                {
                    WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Username not provided.");
                }
                if (tbPassword.Text == String.Empty)
                {
                    WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: ULO Password not provided.");
                }
                if (cbAction.Items[cbAction.SelectedIndex].ToString() == String.Empty)
                {
                    WriteOutput(DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine + "ERROR: Action not provided.");
                }
            }
        }

        private void text_Received(object sender, DataReceivedEventArgs e)
        {
            WriteOutput(e.Data);
        }

        private void WriteOutput(string text)
        {
            if (tbOutput.InvokeRequired)
            {
                var call = new SafeCallDelegate(WriteOutput);
                tbOutput.Invoke(call, new object[] { text });
            }
            else
            {
                tbOutput.Text += text + Environment.NewLine;
                tbOutput.SelectionStart = tbOutput.Text.Length;
                tbOutput.SelectionLength = 0;
            }
        }

        private bool ProcessExists(int pid)
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
            if (!_init)
            {
                CheckBox checkbox = (CheckBox)sender;
                string cfgName = checkbox.Name.Replace("cfg_", String.Empty);
                bool cfgValue = checkbox.Checked;
                
                foreach (FieldInfo fieldInfo in _ulo.configuration.GetType().GetFields())
                {
                    if (fieldInfo.Name == cfgName)
                    {
                        fieldInfo.SetValue(_ulo.configuration, cfgValue);
                    }
                }

                _ulo.WriteConfig();
                _ulo.ReloadConfiguration();
                SetCfgValues();
            }
        }

        private void ControllerGUI_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_streamRunning)
            {
                CancelStream();
            }
            _ulo.HandleTempLogs();
            Application.Exit();
        }
    }
}