using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ULOControls;

namespace ULOController
{
    static class Controller
    {
        public static readonly string productRoot = Path.GetPathRoot(Assembly.GetEntryAssembly()?.Location);
        public static readonly string productLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        public static readonly string productTitle = ((AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0]).Title;
        public static readonly string productVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
        public static readonly string productFilename = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName);
        public static readonly string productConfigFilename = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName) + ".exe.config";

        private static string _action = String.Empty;

        private static readonly Ulo Ulo = new Ulo();

        private static bool _guiShown = false;
        
        public class Actions
        {
            // Developement actions
            public const string CallApi = "callapi";
            // User actions
            public const string GetName = "getname";
            public const string GetMode = "getmode";
            public const string SetMode = "setmode";
            public const string IsPowered = "ispowered";
            public const string GetBattery = "getbattery";
            public const string IsCard = "iscard";
            public const string GetCardSpace = "getcardspace";
            public const string GetDiskSpace = "getdiskspace";
            public const string MoveToCard = "movetocard";
            public const string CleanDiskSpace = "cleandiskspace"; // Admin only
            public const string DownloadLog = "downloadlog";
            public const string CurrentSnapshot = "currentsnapshot";
            public const string DownloadVideos = "downloadvideos";
            public const string DownloadSnapshots = "downloadsnapshots";
            public const string TestAvailability = "testavailability";
            public const string CheckAvailability = "checkavailability";
            // WebSocket live feed
            public const string LiveFeed = "livefeed";
        }

        public class DeletePeriodMap
        {
            public const string OldestDay = "oldestday";
            public const string OldestWeek = "oldestweek";
            public const string OldestYear = "oldestyear";
            public const string LatestDay = "latestday";
            public const string LatestWeek = "latestweek";
            public const string LatestYear = "latestyear";
            public const string All = "all";
        }

        public static string Usage()
        {
            string usage = String.Empty;
            /* Length limit - mind the variables, their values and escape symbols */
            usage += productTitle + @" v" + productVersion + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Usage:" + Environment.NewLine;
            usage += @"   ./" + productFilename + @" <ulo_host> <ulo_user> <ulo_pass> <action> <arg1> <argN>" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Actions:" + Environment.NewLine;
            usage += @"   " + Actions.LiveFeed + @" - Show current live feed from camera (GUI only)" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. store feed - 0 (off) / 1 (on) [Default: 0]" + Environment.NewLine;
            usage += @"           2. destination path - location where recorded files should be" + Environment.NewLine;
            usage += @"                                 stored [Default: <executable location>/media]" + Environment.NewLine;
            usage += @"           3. maximum file size - size in MB above which recorded file" + Environment.NewLine;
            usage += @"                                  will be split [Default: 100]" + Environment.NewLine;
            usage += @"           4. retention - number of split files that wont be automatically" + Environment.NewLine;
            usage += @"                          deleted [Default: 5]" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.GetName + @" - Get ULO name" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"   " + Actions.GetMode + @" - Get current ULO camera mode" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.SetMode + @" - Set ULO camera mode" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. mode - camera recording mode" + Environment.NewLine;
            usage += @"               a) " + Ulo.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + Ulo.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + Ulo.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.IsPowered + @" - Get info if ULO is powered by electricity from plug" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.GetBattery + @" - Get battery capacity" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.IsCard + @" - Get info if SD card is inserted into ULO" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.GetCardSpace + @" - Get SD card free capacity" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.GetDiskSpace + @" - Get internal memory free capacity" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.MoveToCard + @" - Move files from internal memory to SD card" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"                  NOTE: ULO cannot record during this activity." + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.CleanDiskSpace + @" - Clean files on internal memory" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. period - how old/new files should be deleted" + Environment.NewLine;
            usage += @"                       NOTE: This action requires admin account and ULO cannot" + Environment.NewLine;
            usage += @"                             record during this activity." + Environment.NewLine;
            usage += @"               a) " + DeletePeriodMap.OldestDay + @" - Oldest day" + Environment.NewLine;
            usage += @"               b) " + DeletePeriodMap.OldestWeek + @" - Oldest week" + Environment.NewLine;
            usage += @"               c) " + DeletePeriodMap.OldestYear + @" - Oldest year" + Environment.NewLine;
            usage += @"               d) " + DeletePeriodMap.LatestDay + @" - Latest day" + Environment.NewLine;
            usage += @"               e) " + DeletePeriodMap.LatestWeek + @" - Latest week" + Environment.NewLine;
            usage += @"               f) " + DeletePeriodMap.LatestYear + @" - Latest year" + Environment.NewLine;
            usage += @"               g) " + DeletePeriodMap.All + @" - All" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.DownloadLog + @" - Download ULO log into specified location" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. destination type - " + Ulo.DestinationType.Local + @", " + Ulo.DestinationType.Nfs + @", " + Ulo.DestinationType.Ftp + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Always use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + Ulo.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + Ulo.DestinationType.Nfs + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + Ulo.DestinationType.Ftp + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
            usage += @"                        password)" + Environment.NewLine;
            usage += @"           3. retention - how old uploaded files should be removed in hours;" + Environment.NewLine;
            usage += @"                          if set to 0, no age limit will be used and all" + Environment.NewLine;
            usage += @"                          files will be kept" + Environment.NewLine;
            usage += @"           4. username" + Environment.NewLine;
            usage += @"           5. password" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.CurrentSnapshot + @" - Download current snapshot seen by ULO into specified" + Environment.NewLine;
            usage += @"                     location, if snapshot with same name exists it" + Environment.NewLine;
            usage += @"                     is overwritten" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. destination type - " + Ulo.DestinationType.Local + @", " + Ulo.DestinationType.Nfs + @", " + Ulo.DestinationType.Ftp + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Always use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + Ulo.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + Ulo.DestinationType.Nfs + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + Ulo.DestinationType.Ftp + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
            usage += @"                        password)" + Environment.NewLine;
            usage += @"           3. username" + Environment.NewLine;
            usage += @"           4. password" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.DownloadVideos + @" - Download all available videos stored in ULO into specified" + Environment.NewLine;
            usage += @"                    location, if video with same name exists it is skipped" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. destination type - " + Ulo.DestinationType.Local + @", " + Ulo.DestinationType.Nfs + @", " + Ulo.DestinationType.Ftp + Environment.NewLine;
            usage += @"           2. destination path - location where video files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Always use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + Ulo.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + Ulo.DestinationType.Nfs + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + Ulo.DestinationType.Ftp + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
            usage += @"                        password)" + Environment.NewLine;
            usage += @"           3. age - how old files should be downloaded in hours; if set" + Environment.NewLine;
            usage += @"                    to 0, no age limit will be used and all files will" + Environment.NewLine;
            usage += @"                    be downloaded" + Environment.NewLine;
            usage += @"           4. retention - how old uploaded files should be removed in hours;" + Environment.NewLine;
            usage += @"                          if set to 0, no age limit will be used and all" + Environment.NewLine;
            usage += @"                          files will be kept" + Environment.NewLine;
            usage += @"           4. username" + Environment.NewLine;
            usage += @"           5. password" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.DownloadSnapshots + @" - Download all available snapshots stored in ULO into" + Environment.NewLine;
            usage += @"                       specified location, if snapshot with same name exists it" + Environment.NewLine;
            usage += @"                       is skipped" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. destination type - " + Ulo.DestinationType.Local + @", " + Ulo.DestinationType.Nfs + @", " + Ulo.DestinationType.Ftp + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Always use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + Ulo.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + Ulo.DestinationType.Nfs + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + Ulo.DestinationType.Ftp + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
            usage += @"                        password)" + Environment.NewLine;
            usage += @"           3. age - how old files should be downloaded in hours; if set" + Environment.NewLine;
            usage += @"                    to 0, no age limit will be used and all files will" + Environment.NewLine;
            usage += @"                    be downloaded" + Environment.NewLine;
            usage += @"           4. retention - how old uploaded files should be removed in hours;" + Environment.NewLine;
            usage += @"                          if set to 0, no age limit will be used and all" + Environment.NewLine;
            usage += @"                          files will be kept" + Environment.NewLine;
            usage += @"           4. username" + Environment.NewLine;
            usage += @"           5. password" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.TestAvailability + @" - Test for device availability" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. host - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.CheckAvailability + @" - Check for device availability and set proper mode" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. mode if true - camera recording mode if conditions are met" + Environment.NewLine;
            usage += @"               a) " + Ulo.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + Ulo.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + Ulo.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
            usage += @"           2. mode if false - camera recording mode if conditions are not met" + Environment.NewLine;
            usage += @"               a) " + Ulo.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + Ulo.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + Ulo.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
            usage += @"           3. operation - operation to determine how to check devices" + Environment.NewLine;
            usage += @"               a) " + Ulo.Operation.And + @" - All devices available to be true" + Environment.NewLine;
            usage += @"               b) " + Ulo.Operation.Or + @" - Any device available to be true" + Environment.NewLine;
            usage += @"           4. host1 - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"           5. host2 - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"                      (optional)" + Environment.NewLine;
            usage += @"           6. host3 - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"                      (optional)" + Environment.NewLine;
            usage += @"           7. host4 - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"                      (optional)" + Environment.NewLine;
            usage += @"           8. host5 - hostname of device you want to check if available" + Environment.NewLine;
            usage += @"                      (optional)" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.CallApi + @" - Call API with custom parameters" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. api path - path to API module" + Environment.NewLine;
            usage += @"           2. method - call method [GET|PUT|POST|DELETE|...]" + Environment.NewLine;
            usage += @"           3. body - body this might be needed by API but is undocumented" + Environment.NewLine;
            usage += @"           4. json path - JSON path or $ for all" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Examples:" + Environment.NewLine;
            usage += @"    - Download video files" + Environment.NewLine;
            usage += @"        ./" + productFilename + @" ""192.168.0.10"" ""test"" ""123!Abc"" ""downloadvideos""" + Environment.NewLine;
            usage += @"         ""local"" ""C:\ulo\"" ""24"" ""48""" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Library configuration (optional):" + Environment.NewLine;
            usage += @"   By creating a text file with name """ + Path.GetFileName(Ulo.confFile) + @""" in same directory as ULO" + Environment.NewLine;
            usage += @"   library, you can change some library behavior or enable debug options. Each" + Environment.NewLine;
            usage += @"   parameter can be set to either true or false, there can be only one parameter" + Environment.NewLine;
            usage += @"   per line and there should be equal sign (=) between parameter name and value." + Environment.NewLine;
            usage += @"   Default values are false." + Environment.NewLine;
            usage += @"       1. " + Ulo.ConfigParams.WriteLog + @" - write output into log file" + Environment.NewLine;
            usage += @"       2. " + Ulo.ConfigParams.ShowArguments + @" - incoming arguments will be written to console" + Environment.NewLine;
            usage += @"       3. " + Ulo.ConfigParams.ShowTrace + @" - error trace will be written to console" + Environment.NewLine;
            usage += @"       4. " + Ulo.ConfigParams.ShowSkipped + @" - skipped files will be written to log and console" + Environment.NewLine;
            usage += @"       5. " + Ulo.ConfigParams.ShowPingResults + @" - availability check will show more information" + Environment.NewLine;
            usage += @"       6. " + Ulo.ConfigParams.SuppressLogHandling + @" - log handler will stop chronologically push logs" + Environment.NewLine;
            usage += @"                                into single log file" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Examples:" + Environment.NewLine;
            usage += @"    " + Ulo.ConfigParams.WriteLog + @"=true" + Environment.NewLine;
            usage += @"    " + Ulo.ConfigParams.ShowArguments + @"=false" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Notes from working with ULO:" + Environment.NewLine;
            usage += @"    - When using this tool, ULO usually wakes up unless it is in Alert mode." + Environment.NewLine;
            usage += @"    - Transfer speeds usually depends on WiFi signal strength or ULOs" + Environment.NewLine;
            usage += @"      processing power. Due to way how we access files there is not much space" + Environment.NewLine;
            usage += @"      to make this process faster in this code." + Environment.NewLine;
            usage += @"    - Files from ULO memory can be emptied only in standard mode." + Environment.NewLine;
            usage += @"    - This tool properly logs in and logs out into ULO; because of this," + Environment.NewLine;
            usage += @"      if you use same user for browser access this user will be logged" + Environment.NewLine;
            usage += @"      out along with this tool at the end of execution." + Environment.NewLine;
            usage += @"    - It is advised to create new user without admin privileges to use this" + Environment.NewLine;
            usage += @"      tool, unless you need to perform tasks that require them. For now" + Environment.NewLine;
            usage += @"      it seems that ULO can create multiple users, but they sometimes have" + Environment.NewLine;
            usage += @"      problems to log in." + Environment.NewLine;
            usage += @"    - If multiple activities are performed at a same time or their execution" + Environment.NewLine;
            usage += @"      might overlap, it is advised to create separate ULO users for such" + Environment.NewLine;
            usage += @"      activities." + Environment.NewLine;
            usage += @"    - NFS cannot be both used in Windows and used by script, if used so," + Environment.NewLine;
            usage += @"      one or the other might stop working after some time." + Environment.NewLine;
            usage += @"    - FTP upload supports anonymous login." + Environment.NewLine;
            usage += @"    - FTP is very permission sensitive, wrongly set permissions may lead to" + Environment.NewLine;
            usage += @"      some features returning errors." + Environment.NewLine;
            usage += @"    - ULO can perform unintended self reboots which always reset current" + Environment.NewLine;
            usage += @"      camera mode to standard and therefore ULO will stop recording." + Environment.NewLine;
            usage += @"    - In version 10.1308 and maybe earlier, there is a bug where anyone who" + Environment.NewLine;
            usage += @"      knows about ULO can access all ULO files even when not logged in to ULO," + Environment.NewLine;
            usage += @"      when at least one user is logged in to ULO no matter where." + Environment.NewLine;
            usage += @"    - In version 10.1308 and maybe earlier, ULO stores WiFi passwords in" + Environment.NewLine;
            usage += @"      plain text inside its system log which is accessible if requested." + Environment.NewLine;

            return usage;
        }

        /// <summary>
        /// Process input arguments into variables and log/output info about it if configured so
        /// </summary>
        private static string HandleArg(string[] args, int index, string ifNotSet)
        {
            string output = String.Empty;
            bool usedDefault = false;

            try
            {
                output = args[index].Trim('\'').Trim('"');
            }
            catch (Exception ex)
            {
                output = ifNotSet;
                usedDefault = true;
            }

            // Action should be stored as global variable
            if (index == 3)
            {
                _action = output.ToLower();
            }

            if (Ulo.configuration.showArguments)
            {
                if (index == 0)
                {
                    Ulo.WriteLog(Ulo.tempOutFile, String.Empty, true);
                    Ulo.WriteLog(Ulo.tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);
                }

                // Check if passwords are not processed and remove them
                string revisedOutput = output;
                string passwordStars = "****";

                if (output != String.Empty)
                {
                    switch (index)
                    {
                        case 2:
                            // ULO password
                            revisedOutput = passwordStars;
                            break;
                        default:
                            switch (_action)
                            {
                                case Actions.DownloadLog:
                                    // Destination password
                                    if (index == 9) { revisedOutput = passwordStars; }
                                    break;
                                case Actions.CurrentSnapshot:
                                    // Destination password
                                    if (index == 7) { revisedOutput = passwordStars; }
                                    break;
                                case Actions.DownloadVideos:
                                    // Destination password
                                    if (index == 9) { revisedOutput = passwordStars; }
                                    break;
                                case Actions.DownloadSnapshots:
                                    // Destination password
                                    if (index == 9) { revisedOutput = passwordStars; }
                                    break;
                            }
                            break;
                    }
                }

                Ulo.WriteLog(Ulo.tempOutFile, "Argument " + ((index < 10) ? "0" + index : index.ToString()) + " = '" + revisedOutput + "' (used_default: " + usedDefault + ")", true);
            }

            return output;
        }

        /// <summary>
        /// Call controller actions to manipulate ULO
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Define arguments
                string host = HandleArg(args, 0, String.Empty);
                string username = HandleArg(args, 1, String.Empty);
                string password = HandleArg(args, 2, String.Empty);
                string action = HandleArg(args, 3, String.Empty);
                string arg1 = HandleArg(args, 4, String.Empty);
                string arg2 = HandleArg(args, 5, String.Empty);
                string arg3 = HandleArg(args, 6, String.Empty);
                string arg4 = HandleArg(args, 7, String.Empty);
                string arg5 = HandleArg(args, 8, String.Empty);
                string arg6 = HandleArg(args, 9, String.Empty);
                string arg7 = HandleArg(args, 10, String.Empty);
                string arg8 = HandleArg(args, 11, String.Empty);

                if (Ulo.configuration.showArguments)
                {
                    Ulo.WriteLog(Ulo.tempOutFile, String.Empty, true);
                }
                
                // Main execution
                if (host == String.Empty)
                {
                    if (Environment.UserInteractive)
                    {
                        _guiShown = true;
                        // Show GUI
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new ControllerGui());
                    }
                    else
                    {
                        // No arguments, find who called it and perform appropriate action
                        Process parent = ParentProcessUtilities.GetParentProcess();
                        //Console.WriteLine("Parent name: " + parent.ProcessName);
                        if (parent.ProcessName == "explorer")
                        {
                            // If explorer started this application, open CMD
                            Process process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            process.StartInfo.UseShellExecute = true;
                            process.Start();
                            // Alternative is to run GUI here (maybe in the distant future) - Application output type has to be changed to Windows Application
                        }
                        else
                        {
                            // If applicaation is not started by explorer show help
                            Console.WriteLine(Usage());
                        }
                    }
                }
                else if(host == "/?" || host == "?" || host == "-h" || host == "-help" || host == "--help")
                {
                    Console.WriteLine(Usage());
                }
                else
                {
                    try
                    {
                        // Login
                        Ulo.Login(host, username, password);

                        // Perform action
                        Ulo.WriteLog(Ulo.tempOutFile, ExecuteAction(action, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8), true);

                        Ulo.Logout();
                    }
                    catch (Exception ex)
                    {
                        Ulo.Logout();

                        throw;
                    }

                }

                Ulo.HandleTempLogs();
            }
            catch (Exception ex)
            {
                if (_guiShown)
                {
                    throw;
                }

                string dot = String.Empty;
                if (ex.Message.TrimEnd('.').Length == ex.Message.Length)
                {
                    dot = ".";
                }
                Console.WriteLine(@"ERROR: " + ex.Message + dot);
                ExceptionHandler(ex);

                try
                {
                    Ulo.HandleTempLogs();
                }
                catch (Exception exIn)
                {
                    Ulo.MarkLogs();
                }
            }
        }

        public static string ExecuteAction(string action, string arg1, string arg2, string arg3, string arg4, string arg5, string arg6, string arg7, string arg8)
        {
            string output = String.Empty;

            switch (action.ToLower())
            {
                // Developement actions
                case Actions.CallApi:
                    // Get API output
                    output = Ulo.CallApi(arg1, arg2, arg3, arg4);
                    break;
                // User actions
                case Actions.GetName:
                    // Get name
                    output = Ulo.GetName();
                    break;
                case Actions.GetMode:
                    // Get mode
                    output = Ulo.GetMode();
                    break;
                case Actions.SetMode:
                    // Set mode
                    switch (arg1.ToLower())
                    {
                        case Ulo.CameraMode.Standard:
                            Ulo.SetMode(Ulo.CameraMode.Standard);
                            break;
                        case Ulo.CameraMode.Spy:
                            Ulo.SetMode(Ulo.CameraMode.Spy);
                            break;
                        case Ulo.CameraMode.Alert:
                            Ulo.SetMode(Ulo.CameraMode.Alert);
                            break;
                        default:
                            throw new Exception("Mode '" + arg1 + "' is not supported.");
                    }
                    break;
                case Actions.IsPowered:
                    // Get info if ULO is powered by electricity from plug
                    output = Convert.ToString(Ulo.IsPowered());
                    break;
                case Actions.GetBattery:
                    // Get battery capacity
                    output = Convert.ToString(Ulo.GetBattery());
                    break;
                case Actions.IsCard:
                    // Get info if SD card is inserted into ULO
                    output = Convert.ToString(Ulo.IsCard());
                    break;
                case Actions.GetCardSpace:
                    // Get SD card free capacity
                    output = Convert.ToString(Ulo.GetCardSpace());
                    break;
                case Actions.GetDiskSpace:
                    // Get internal memory free capacity
                    output = Convert.ToString(Ulo.GetDiskSpace());
                    break;
                case Actions.MoveToCard:
                    // Move files from internal memory to SD card
                    Ulo.MoveToCard();
                    break;
                case Actions.CleanDiskSpace:
                    // Clean files on internal memory
                    string period = String.Empty;
                    switch (arg1.ToLower())
                    {
                        case DeletePeriodMap.OldestDay:
                            period = Ulo.DeletePeriod.OldestDay;
                            break;
                        case DeletePeriodMap.OldestWeek:
                            period = Ulo.DeletePeriod.OldestWeek;
                            break;
                        case DeletePeriodMap.OldestYear:
                            period = Ulo.DeletePeriod.OldestYear;
                            break;
                        case DeletePeriodMap.LatestDay:
                            period = Ulo.DeletePeriod.LatestDay;
                            break;
                        case DeletePeriodMap.LatestWeek:
                            period = Ulo.DeletePeriod.LatestWeek;
                            break;
                        case DeletePeriodMap.LatestYear:
                            period = Ulo.DeletePeriod.LatestYear;
                            break;
                        case DeletePeriodMap.All:
                            period = Ulo.DeletePeriod.All;
                            break;
                        default:
                            throw new Exception("Delete period '" + arg1 + "' is not supported.");
                    }
                    Ulo.CleanDiskSpace(period);
                    break;
                case Actions.DownloadLog:
                    // Download ULO log into specified location
                    Ulo.DownloadLog(arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), arg4, arg5);
                    break;
                case Actions.CurrentSnapshot:
                    // Download current snapshot
                    Ulo.DownloadCurrent(arg1, arg2, arg3, arg4);
                    break;
                case Actions.DownloadVideos:
                    // Download videos
                    Ulo.DownloadMedia(Ulo.MediaType.Video, arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), ((arg4 != String.Empty) ? Int32.Parse(arg4) : 0), arg5, arg6);
                    break;
                case Actions.DownloadSnapshots:
                    // Download snapshots
                    Ulo.DownloadMedia(Ulo.MediaType.Snapshot, arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), ((arg4 != String.Empty) ? Int32.Parse(arg4) : 0), arg5, arg6);
                    break;
                case Actions.TestAvailability:
                    // Test for device availability
                    Ulo.TestAvailability(arg1);
                    break;
                case Actions.CheckAvailability:
                    // Check for device availability and set proper mode
                    string modeIfTrue = "";
                    switch (arg1.ToLower())
                    {
                        case Ulo.CameraMode.Standard:
                            modeIfTrue = Ulo.CameraMode.Standard;
                            break;
                        case Ulo.CameraMode.Spy:
                            modeIfTrue = Ulo.CameraMode.Spy;
                            break;
                        case Ulo.CameraMode.Alert:
                            modeIfTrue = Ulo.CameraMode.Alert;
                            break;
                        default:
                            throw new Exception("Mode '" + arg1 + "' is not supported.");
                    }

                    string modeIfFalse = "";
                    switch (arg2.ToLower())
                    {
                        case Ulo.CameraMode.Standard:
                            modeIfFalse = Ulo.CameraMode.Standard;
                            break;
                        case Ulo.CameraMode.Spy:
                            modeIfFalse = Ulo.CameraMode.Spy;
                            break;
                        case Ulo.CameraMode.Alert:
                            modeIfFalse = Ulo.CameraMode.Alert;
                            break;
                        default:
                            throw new Exception("Mode '" + arg2 + "' is not supported.");
                    }
                    
                    string operation = "";
                    switch (arg3.ToLower())
                    {
                        case Ulo.Operation.And:
                            operation = Ulo.Operation.And;
                            break;
                        case Ulo.Operation.Or:
                            operation = Ulo.Operation.Or;
                            break;
                        default:
                            throw new Exception("Operation '" + arg3 + "' is not supported.");
                    }

                    Ulo.CheckAvailability(modeIfTrue, modeIfFalse, operation, arg4, arg5, arg6, arg7, arg8);
                    break;
                case Actions.LiveFeed:
                    throw new Exception("Live feed is GUI only feature.");
                default:
                    throw new Exception("Action '" + action + "' is not supported.");
            }
            
            return output;
        }

        private static void ExceptionHandler(Exception ex)
        {
            string timestamp = DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]");
            string errorOutput = String.Empty;
            errorOutput += timestamp + Environment.NewLine;
            errorOutput += "HelpLink   = " + ex.HelpLink + Environment.NewLine;
            errorOutput += "Message    = " + ex.Message + Environment.NewLine;
            errorOutput += "Source     = " + ex.Source + Environment.NewLine;
            errorOutput += "StackTrace = " + ex.StackTrace + Environment.NewLine;
            errorOutput += "TargetSite = " + ex.TargetSite + Environment.NewLine;

            if (Ulo.configuration.showTrace)
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine(errorOutput);
            }

            Ulo.WriteLog(Ulo.tempErrFile, errorOutput, true, true);
            Ulo.WriteLog(Ulo.tempOutFile, timestamp + " ERROR: " + ex.Message, true, true);

            //throw ex;
        }
    }
}

/// <summary>
/// A utility class to determine a process parent.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ParentProcessUtilities
{
    // These members must match PROCESS_BASIC_INFORMATION
    internal IntPtr Reserved1;
    internal IntPtr PebBaseAddress;
    internal IntPtr Reserved2_0;
    internal IntPtr Reserved2_1;
    internal IntPtr UniqueProcessId;
    internal IntPtr InheritedFromUniqueProcessId;

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);

    /// <summary>
    /// Gets the parent process of the current process.
    /// </summary>
    /// <returns>An instance of the Process class.</returns>
    public static Process GetParentProcess()
    {
        return GetParentProcess(Process.GetCurrentProcess().Handle);
    }

    /// <summary>
    /// Gets the parent process of specified process.
    /// </summary>
    /// <param name="id">The process id.</param>
    /// <returns>An instance of the Process class.</returns>
    public static Process GetParentProcess(int id)
    {
        Process process = Process.GetProcessById(id);
        return GetParentProcess(process.Handle);
    }

    /// <summary>
    /// Gets the parent process of a specified process.
    /// </summary>
    /// <param name="handle">The process handle.</param>
    /// <returns>An instance of the Process class.</returns>
    public static Process GetParentProcess(IntPtr handle)
    {
        ParentProcessUtilities pbi = new ParentProcessUtilities();
        int returnLength;
        int status = NtQueryInformationProcess(handle, 0, ref pbi, Marshal.SizeOf(pbi), out returnLength);
        if (status != 0)
            throw new Win32Exception(status);

        try
        {
            return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
        }
        catch (ArgumentException)
        {
            // not found
            return null;
        }
    }
}
