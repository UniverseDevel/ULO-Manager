namespace ULOController
{
    partial class ControllerGUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ControllerGUI));
            this.label1 = new System.Windows.Forms.Label();
            this.cbAction = new System.Windows.Forms.ComboBox();
            this.tbHost = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbUsername = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbPassword = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbArg1 = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tbArg2 = new System.Windows.Forms.TextBox();
            this.tbArg3 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.tbArg4 = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.tbArg8 = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.tbArg7 = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.tbArg6 = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.tbArg5 = new System.Windows.Forms.TextBox();
            this.btnExecute = new System.Windows.Forms.Button();
            this.cfg_writeLog = new System.Windows.Forms.CheckBox();
            this.cfg_showArguments = new System.Windows.Forms.CheckBox();
            this.cfg_showTrace = new System.Windows.Forms.CheckBox();
            this.cfg_showSkipped = new System.Windows.Forms.CheckBox();
            this.cfg_showPingResults = new System.Windows.Forms.CheckBox();
            this.cfg_suppressLogHandling = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.tbUsage = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tbOutput = new System.Windows.Forms.TextBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.btnCancel = new System.Windows.Forms.Button();
            this.videoView1 = new LibVLCSharp.WinForms.VideoView();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.videoView1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(54, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "ULO Host";
            // 
            // cbAction
            // 
            this.cbAction.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbAction.Location = new System.Drawing.Point(66, 43);
            this.cbAction.Name = "cbAction";
            this.cbAction.Size = new System.Drawing.Size(247, 21);
            this.cbAction.TabIndex = 1;
            // 
            // tbHost
            // 
            this.tbHost.Location = new System.Drawing.Point(66, 17);
            this.tbHost.Name = "tbHost";
            this.tbHost.Size = new System.Drawing.Size(247, 20);
            this.tbHost.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(325, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(80, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "ULO Username";
            // 
            // tbUsername
            // 
            this.tbUsername.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tbUsername.Location = new System.Drawing.Point(405, 17);
            this.tbUsername.Name = "tbUsername";
            this.tbUsername.Size = new System.Drawing.Size(249, 20);
            this.tbUsername.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(325, 46);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(78, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "ULO Password";
            // 
            // tbPassword
            // 
            this.tbPassword.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tbPassword.Location = new System.Drawing.Point(405, 43);
            this.tbPassword.Name = "tbPassword";
            this.tbPassword.Size = new System.Drawing.Size(249, 20);
            this.tbPassword.TabIndex = 8;
            this.tbPassword.UseSystemPasswordChar = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 46);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(37, 13);
            this.label5.TabIndex = 11;
            this.label5.Text = "Action";
            // 
            // tbArg1
            // 
            this.tbArg1.Location = new System.Drawing.Point(42, 19);
            this.tbArg1.Name = "tbArg1";
            this.tbArg1.Size = new System.Drawing.Size(115, 20);
            this.tbArg1.TabIndex = 12;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(4, 22);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(32, 13);
            this.label6.TabIndex = 13;
            this.label6.Text = "Arg 1";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(166, 22);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(32, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "Arg 2";
            // 
            // tbArg2
            // 
            this.tbArg2.Location = new System.Drawing.Point(204, 19);
            this.tbArg2.Name = "tbArg2";
            this.tbArg2.Size = new System.Drawing.Size(115, 20);
            this.tbArg2.TabIndex = 16;
            // 
            // tbArg3
            // 
            this.tbArg3.Location = new System.Drawing.Point(372, 19);
            this.tbArg3.Name = "tbArg3";
            this.tbArg3.Size = new System.Drawing.Size(115, 20);
            this.tbArg3.TabIndex = 18;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(334, 22);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(32, 13);
            this.label8.TabIndex = 17;
            this.label8.Text = "Arg 3";
            // 
            // tbArg4
            // 
            this.tbArg4.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tbArg4.Location = new System.Drawing.Point(539, 19);
            this.tbArg4.Name = "tbArg4";
            this.tbArg4.Size = new System.Drawing.Size(115, 20);
            this.tbArg4.TabIndex = 20;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(501, 21);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(32, 13);
            this.label9.TabIndex = 19;
            this.label9.Text = "Arg 4";
            // 
            // tbArg8
            // 
            this.tbArg8.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tbArg8.Location = new System.Drawing.Point(539, 45);
            this.tbArg8.Name = "tbArg8";
            this.tbArg8.Size = new System.Drawing.Size(115, 20);
            this.tbArg8.TabIndex = 28;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(501, 48);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(32, 13);
            this.label10.TabIndex = 27;
            this.label10.Text = "Arg 8";
            // 
            // tbArg7
            // 
            this.tbArg7.Location = new System.Drawing.Point(372, 45);
            this.tbArg7.Name = "tbArg7";
            this.tbArg7.Size = new System.Drawing.Size(115, 20);
            this.tbArg7.TabIndex = 26;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(334, 48);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(32, 13);
            this.label11.TabIndex = 25;
            this.label11.Text = "Arg 7";
            // 
            // tbArg6
            // 
            this.tbArg6.Location = new System.Drawing.Point(204, 45);
            this.tbArg6.Name = "tbArg6";
            this.tbArg6.Size = new System.Drawing.Size(115, 20);
            this.tbArg6.TabIndex = 24;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(166, 48);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(32, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "Arg 6";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(4, 48);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(32, 13);
            this.label13.TabIndex = 22;
            this.label13.Text = "Arg 5";
            // 
            // tbArg5
            // 
            this.tbArg5.Location = new System.Drawing.Point(42, 45);
            this.tbArg5.Name = "tbArg5";
            this.tbArg5.Size = new System.Drawing.Size(115, 20);
            this.tbArg5.TabIndex = 21;
            // 
            // btnExecute
            // 
            this.btnExecute.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExecute.Location = new System.Drawing.Point(12, 232);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(660, 23);
            this.btnExecute.TabIndex = 29;
            this.btnExecute.Text = "Execute";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // cfg_writeLog
            // 
            this.cfg_writeLog.AutoSize = true;
            this.cfg_writeLog.Location = new System.Drawing.Point(6, 19);
            this.cfg_writeLog.Name = "cfg_writeLog";
            this.cfg_writeLog.Size = new System.Drawing.Size(72, 17);
            this.cfg_writeLog.TabIndex = 30;
            this.cfg_writeLog.Text = "Write Log";
            this.cfg_writeLog.UseVisualStyleBackColor = true;
            this.cfg_writeLog.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // cfg_showArguments
            // 
            this.cfg_showArguments.AutoSize = true;
            this.cfg_showArguments.Location = new System.Drawing.Point(6, 42);
            this.cfg_showArguments.Name = "cfg_showArguments";
            this.cfg_showArguments.Size = new System.Drawing.Size(106, 17);
            this.cfg_showArguments.TabIndex = 31;
            this.cfg_showArguments.Text = "Show Arguments";
            this.cfg_showArguments.UseVisualStyleBackColor = true;
            this.cfg_showArguments.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // cfg_showTrace
            // 
            this.cfg_showTrace.AutoSize = true;
            this.cfg_showTrace.Location = new System.Drawing.Point(138, 19);
            this.cfg_showTrace.Name = "cfg_showTrace";
            this.cfg_showTrace.Size = new System.Drawing.Size(84, 17);
            this.cfg_showTrace.TabIndex = 32;
            this.cfg_showTrace.Text = "Show Trace";
            this.cfg_showTrace.UseVisualStyleBackColor = true;
            this.cfg_showTrace.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // cfg_showSkipped
            // 
            this.cfg_showSkipped.AutoSize = true;
            this.cfg_showSkipped.Location = new System.Drawing.Point(138, 42);
            this.cfg_showSkipped.Name = "cfg_showSkipped";
            this.cfg_showSkipped.Size = new System.Drawing.Size(95, 17);
            this.cfg_showSkipped.TabIndex = 33;
            this.cfg_showSkipped.Text = "Show Skipped";
            this.cfg_showSkipped.UseVisualStyleBackColor = true;
            this.cfg_showSkipped.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // cfg_showPingResults
            // 
            this.cfg_showPingResults.AutoSize = true;
            this.cfg_showPingResults.Location = new System.Drawing.Point(260, 19);
            this.cfg_showPingResults.Name = "cfg_showPingResults";
            this.cfg_showPingResults.Size = new System.Drawing.Size(115, 17);
            this.cfg_showPingResults.TabIndex = 34;
            this.cfg_showPingResults.Text = "Show Ping Results";
            this.cfg_showPingResults.UseVisualStyleBackColor = true;
            this.cfg_showPingResults.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // cfg_suppressLogHandling
            // 
            this.cfg_suppressLogHandling.AutoSize = true;
            this.cfg_suppressLogHandling.Location = new System.Drawing.Point(260, 42);
            this.cfg_suppressLogHandling.Name = "cfg_suppressLogHandling";
            this.cfg_suppressLogHandling.Size = new System.Drawing.Size(136, 17);
            this.cfg_suppressLogHandling.TabIndex = 35;
            this.cfg_suppressLogHandling.Text = "Suppress Log Handling";
            this.cfg_suppressLogHandling.UseVisualStyleBackColor = true;
            this.cfg_suppressLogHandling.CheckedChanged += new System.EventHandler(this.cfgValue_CheckedChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.tbHost);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.cbAction);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tbUsername);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.tbPassword);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(660, 69);
            this.groupBox1.TabIndex = 36;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Parameters";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.tbArg2);
            this.groupBox2.Controls.Add(this.tbArg1);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.tbArg3);
            this.groupBox2.Controls.Add(this.label9);
            this.groupBox2.Controls.Add(this.tbArg8);
            this.groupBox2.Controls.Add(this.tbArg4);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.tbArg5);
            this.groupBox2.Controls.Add(this.tbArg7);
            this.groupBox2.Controls.Add(this.label13);
            this.groupBox2.Controls.Add(this.label11);
            this.groupBox2.Controls.Add(this.label12);
            this.groupBox2.Controls.Add(this.tbArg6);
            this.groupBox2.Location = new System.Drawing.Point(12, 87);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(660, 69);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Arguments";
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.cfg_writeLog);
            this.groupBox3.Controls.Add(this.cfg_showArguments);
            this.groupBox3.Controls.Add(this.cfg_suppressLogHandling);
            this.groupBox3.Controls.Add(this.cfg_showTrace);
            this.groupBox3.Controls.Add(this.cfg_showPingResults);
            this.groupBox3.Controls.Add(this.cfg_showSkipped);
            this.groupBox3.Location = new System.Drawing.Point(12, 162);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(660, 64);
            this.groupBox3.TabIndex = 37;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Configuration";
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.tbUsage);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(678, 276);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Usage";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // tbUsage
            // 
            this.tbUsage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbUsage.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.tbUsage.Location = new System.Drawing.Point(3, 3);
            this.tbUsage.Multiline = true;
            this.tbUsage.Name = "tbUsage";
            this.tbUsage.ReadOnly = true;
            this.tbUsage.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbUsage.Size = new System.Drawing.Size(672, 270);
            this.tbUsage.TabIndex = 0;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.videoView1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(678, 276);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Live feed";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.tbOutput);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(678, 276);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Output";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tbOutput
            // 
            this.tbOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbOutput.Font = new System.Drawing.Font("Consolas", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.tbOutput.Location = new System.Drawing.Point(3, 3);
            this.tbOutput.Multiline = true;
            this.tbOutput.Name = "tbOutput";
            this.tbOutput.ReadOnly = true;
            this.tbOutput.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbOutput.Size = new System.Drawing.Size(672, 270);
            this.tbOutput.TabIndex = 1;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Location = new System.Drawing.Point(0, 261);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(686, 302);
            this.tabControl1.TabIndex = 9;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.BackColor = System.Drawing.Color.IndianRed;
            this.btnCancel.Location = new System.Drawing.Point(551, 232);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(121, 23);
            this.btnCancel.TabIndex = 38;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Visible = false;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // videoView1
            // 
            this.videoView1.BackColor = System.Drawing.Color.Black;
            this.videoView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.videoView1.Location = new System.Drawing.Point(3, 3);
            this.videoView1.MediaPlayer = null;
            this.videoView1.Name = "videoView1";
            this.videoView1.Size = new System.Drawing.Size(672, 270);
            this.videoView1.TabIndex = 36;
            this.videoView1.Text = "videoView1";
            // 
            // ControllerGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 562);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnExecute);
            this.Controls.Add(this.tabControl1);
            this.Icon = ((System.Drawing.Icon) (resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(700, 600);
            this.Name = "ControllerGUI";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ULO Controller";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ControllerGUI_FormClosed);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize) (this.videoView1)).EndInit();
            this.ResumeLayout(false);
        }

        private LibVLCSharp.WinForms.VideoView videoView1;

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbAction;
        private System.Windows.Forms.TextBox tbHost;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbUsername;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbPassword;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbArg1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tbArg2;
        private System.Windows.Forms.TextBox tbArg3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tbArg4;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox tbArg8;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox tbArg7;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox tbArg6;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox tbArg5;
        private System.Windows.Forms.Button btnExecute;
        private System.Windows.Forms.CheckBox cfg_writeLog;
        private System.Windows.Forms.CheckBox cfg_showArguments;
        private System.Windows.Forms.CheckBox cfg_showTrace;
        private System.Windows.Forms.CheckBox cfg_showSkipped;
        private System.Windows.Forms.CheckBox cfg_showPingResults;
        private System.Windows.Forms.CheckBox cfg_suppressLogHandling;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TextBox tbUsage;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.TextBox tbOutput;
    }
}