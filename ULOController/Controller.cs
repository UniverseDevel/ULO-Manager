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
        public static readonly string product_root = Path.GetPathRoot(Assembly.GetEntryAssembly().Location);
        public static readonly string product_location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static readonly string product_title = ((AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0]).Title;
        public static readonly string product_version = Assembly.GetEntryAssembly().GetName().Version.ToString();
        public static readonly string product_filename = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        public static readonly string product_config_filename = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName) + ".exe.config";

        private static string _action = String.Empty;

        private static ULO ulo = new ULO();

        private static bool gui_shown = false;
        
        public class Actions
        {
            // Developement actions
            public const string CallAPI = "callapi";
            // User actions
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

        public static string usage()
        {
            string usage = String.Empty;
            /* Length limit - mind the variables, their values and escape symbols */
            usage += product_title + @" v" + product_version + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Usage:" + Environment.NewLine;
            usage += @"   ./" + product_filename + @" <ulo_host> <ulo_user> <ulo_pass> <action> <arg1> <argN>" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Actions:" + Environment.NewLine;
            usage += @"   " + Actions.GetMode + @" - Get current ULO camera mode" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           None" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.SetMode + @" - Set ULO camera mode" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. mode - camera recording mode" + Environment.NewLine;
            usage += @"               a) " + ULO.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + ULO.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + ULO.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
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
            usage += @"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Alwayse use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
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
            usage += @"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Alwayse use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
            usage += @"                        password)" + Environment.NewLine;
            usage += @"           3. username" + Environment.NewLine;
            usage += @"           4. password" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"   " + Actions.DownloadVideos + @" - Download all available videos stored in ULO into specified" + Environment.NewLine;
            usage += @"                    location, if video with same name exists it is skipped" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP + Environment.NewLine;
            usage += @"           2. destination path - location where video files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Alwayse use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
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
            usage += @"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP + Environment.NewLine;
            usage += @"           2. destination path - location where snapshot files should be moved" + Environment.NewLine;
            usage += @"                                 NOTE: Alwayse use absolute paths! Destination" + Environment.NewLine;
            usage += @"                                       folder must already exist!" + Environment.NewLine;
            usage += @"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""" + Environment.NewLine;
            usage += @"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)" + Environment.NewLine;
            usage += @"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username," + Environment.NewLine;
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
            usage += @"               a) " + ULO.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + ULO.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + ULO.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
            usage += @"           2. mode if false - camera recording mode if conditions are not met" + Environment.NewLine;
            usage += @"               a) " + ULO.CameraMode.Standard + @" - ULO awake and not recording" + Environment.NewLine;
            usage += @"               b) " + ULO.CameraMode.Spy + @" - ULO awake and recording" + Environment.NewLine;
            usage += @"               c) " + ULO.CameraMode.Alert + @" - ULO asleep and recording" + Environment.NewLine;
            usage += @"           3. operation - operation to determine how to check devices" + Environment.NewLine;
            usage += @"               a) " + ULO.Operation.And + @" - All devices available to be true" + Environment.NewLine;
            usage += @"               b) " + ULO.Operation.Or + @" - Any device available to be true" + Environment.NewLine;
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
            usage += @"   " + Actions.CallAPI + @" - Call API with custom parameters" + Environment.NewLine;
            usage += @"       Arguments:" + Environment.NewLine;
            usage += @"           1. api path - path to API module" + Environment.NewLine;
            usage += @"           2. method - call method [GET|PUT|POST|DELETE|...]" + Environment.NewLine;
            usage += @"           3. body - body this might be needed by API but is undocumented" + Environment.NewLine;
            usage += @"           4. json path - JSON path or $ for all" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Examples:" + Environment.NewLine;
            usage += @"    - Download video files" + Environment.NewLine;
            usage += @"        ./" + product_filename + @" ""192.168.0.10"" ""test"" ""123!Abc"" ""downloadvideos""" + Environment.NewLine;
            usage += @"         ""local"" ""C:\ulo\"" ""24"" ""48""" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Library configuration (optional):" + Environment.NewLine;
            usage += @"   By creating a text file with name """ + Path.GetFileName(ULO.confFile) + @""" in same directory as ULO" + Environment.NewLine;
            usage += @"   library, you can change some library behavior or enable debug options. Each" + Environment.NewLine;
            usage += @"   parameter can be set to either true or false, there can be only one parameter" + Environment.NewLine;
            usage += @"   per line and there should be equal sign (=) between parameter name and value." + Environment.NewLine;
            usage += @"   Default values are false." + Environment.NewLine;
            usage += @"       1. " + ULO.ConfigParams.WriteLog + @" - write output into log file" + Environment.NewLine;
            usage += @"       2. " + ULO.ConfigParams.ShowArguments + @" - incoming arguments will be written to console" + Environment.NewLine;
            usage += @"       3. " + ULO.ConfigParams.ShowTrace + @" - error trace will be written to console" + Environment.NewLine;
            usage += @"       4. " + ULO.ConfigParams.ShowSkipped + @" - skipped files will be written to log and console" + Environment.NewLine;
            usage += @"       5. " + ULO.ConfigParams.ShowPingResults + @" - availability check will show more information" + Environment.NewLine;
            usage += @"       6. " + ULO.ConfigParams.SuppressLogHandling + @" - log handler will stop chronologically push logs" + Environment.NewLine;
            usage += @"                                into single log file" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Examples:" + Environment.NewLine;
            usage += @"    " + ULO.ConfigParams.WriteLog + @"=true" + Environment.NewLine;
            usage += @"    " + ULO.ConfigParams.ShowArguments + @"=false" + Environment.NewLine;
            usage += @"" + Environment.NewLine;
            usage += @"Notes from working with ULO:" + Environment.NewLine;
            usage += @"    - When using this tool, ULO usualy wakes up unless it is in Alert mode." + Environment.NewLine;
            usage += @"    - Transfer speeds usualy depends on WiFi signal strength or ULOs" + Environment.NewLine;
            usage += @"      processing power. Due to way how we access files there is not much space" + Environment.NewLine;
            usage += @"      to make this process faster in this code." + Environment.NewLine;
            usage += @"    - Files from ULO memory can be emptied only in standard mode." + Environment.NewLine;
            usage += @"    - This tool properly logs in and logs out into ULO; because of this," + Environment.NewLine;
            usage += @"      if you use same user for browser access this user will be logged" + Environment.NewLine;
            usage += @"      out along with this tool at the end of execution." + Environment.NewLine;
            usage += @"    - It is advised to create new user without admin privileges to use this" + Environment.NewLine;
            usage += @"      tool, unless you need to perform tasks that require them. For now" + Environment.NewLine;
            usage += @"      it seems that ULO can create mutiple users, but they sometimes have" + Environment.NewLine;
            usage += @"      problems to log in." + Environment.NewLine;
            usage += @"    - If mutiple activities are performed at a same time or their execution" + Environment.NewLine;
            usage += @"      might overlap, it is advised to create separate ULO users for such" + Environment.NewLine;
            usage += @"      activities." + Environment.NewLine;
            usage += @"    - NFS cannot be both used in Windows and used by script, if used so," + Environment.NewLine;
            usage += @"      one or the other might stop working after some time." + Environment.NewLine;
            usage += @"    - FTP upload supports anonymouse login." + Environment.NewLine;
            usage += @"    - FTP is very permission sensitive, wrongly set permissions may lead to" + Environment.NewLine;
            usage += @"      some features returning errors." + Environment.NewLine;
            usage += @"    - ULO can perform unintended self reeboots which always reset current" + Environment.NewLine;
            usage += @"      camera mode to standard and therefore ULO will stop recodring." + Environment.NewLine;
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
        private static string handleArg(string[] args, int index, string if_not_set)
        {
            string output = String.Empty;
            bool used_default = false;

            try
            {
                output = args[index].Trim('\'').Trim('"');
            }
            catch (Exception ex)
            {
                output = if_not_set;
                used_default = true;
            }

            // Action should be stored as global variable
            if (index == 3)
            {
                _action = output.ToLower();
            }

            if (ulo.configuration.showArguments)
            {
                if (index == 0)
                {
                    ulo.writeLog(ULO.tempOutFile, String.Empty, true);
                    ulo.writeLog(ULO.tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);
                }

                // Check if passwords are not processed and remove them
                string revised_output = output;
                string password_stars = "****";

                if (output != String.Empty)
                {
                    switch (index)
                    {
                        case 2:
                            // ULO password
                            revised_output = password_stars;
                            break;
                        default:
                            switch (_action)
                            {
                                case Actions.DownloadLog:
                                    // Destination password
                                    if (index == 9) { revised_output = password_stars; }
                                    break;
                                case Actions.CurrentSnapshot:
                                    // Destination password
                                    if (index == 7) { revised_output = password_stars; }
                                    break;
                                case Actions.DownloadVideos:
                                    // Destination password
                                    if (index == 9) { revised_output = password_stars; }
                                    break;
                                case Actions.DownloadSnapshots:
                                    // Destination password
                                    if (index == 9) { revised_output = password_stars; }
                                    break;
                            }
                            break;
                    }
                }

                ulo.writeLog(ULO.tempOutFile, "Argument " + ((index < 10) ? "0" + index : index.ToString()) + " = '" + revised_output + "' (used_default: " + used_default + ")", true);
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
                string host = handleArg(args, 0, String.Empty);
                string username = handleArg(args, 1, String.Empty);
                string password = handleArg(args, 2, String.Empty);
                string action = handleArg(args, 3, String.Empty);
                string arg1 = handleArg(args, 4, String.Empty);
                string arg2 = handleArg(args, 5, String.Empty);
                string arg3 = handleArg(args, 6, String.Empty);
                string arg4 = handleArg(args, 7, String.Empty);
                string arg5 = handleArg(args, 8, String.Empty);
                string arg6 = handleArg(args, 9, String.Empty);
                string arg7 = handleArg(args, 10, String.Empty);
                string arg8 = handleArg(args, 11, String.Empty);

                if (ulo.configuration.showArguments)
                {
                    ulo.writeLog(ULO.tempOutFile, String.Empty, true);
                }
                
                // Main execution
                if (host == String.Empty)
                {
                    if (Environment.UserInteractive)
                    {
                        gui_shown = true;
                        // Show GUI
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new ControllerGUI());
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
                            Console.WriteLine(usage());
                        }
                    }
                }
                else if(host == "/?" || host == "?" || host == "-h" || host == "-help" || host == "--help")
                {
                    Console.WriteLine(usage());
                }
                else
                {
                    try
                    {
                        // Login
                        ulo.login(host, username, password);

                        // Perform action
                        ulo.writeLog(ULO.tempOutFile, executeAction(action, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8), true);

                        ulo.logout();
                    }
                    catch (Exception ex)
                    {
                        ulo.logout();

                        throw;
                    }

                }

                ulo.handleTempLogs();
            }
            catch (Exception ex)
            {
                if (gui_shown)
                {
                    throw;
                }

                string dot = String.Empty;
                if (ex.Message.TrimEnd('.').Length == ex.Message.Length)
                {
                    dot = ".";
                }
                Console.WriteLine("ERROR: " + ex.Message + dot);
                exceptionHandler(ex);

                try
                {
                    ulo.handleTempLogs();
                }
                catch (Exception ex_in)
                {
                    ulo.markLogs();
                }
            }
        }

        public static string executeAction(string action, string arg1, string arg2, string arg3, string arg4, string arg5, string arg6, string arg7, string arg8)
        {
            string output = String.Empty;

            switch (action.ToLower())
            {
                // Developement actions
                case Actions.CallAPI:
                    // Get API output
                    output = ulo.callAPI(arg1, arg2, arg3, arg4);
                    break;
                // User actions
                case Actions.GetMode:
                    // Get mode
                    output = ulo.getMode();
                    break;
                case Actions.SetMode:
                    // Set mode
                    switch (arg1.ToLower())
                    {
                        case ULO.CameraMode.Standard:
                            ulo.setMode(ULO.CameraMode.Standard);
                            break;
                        case ULO.CameraMode.Spy:
                            ulo.setMode(ULO.CameraMode.Spy);
                            break;
                        case ULO.CameraMode.Alert:
                            ulo.setMode(ULO.CameraMode.Alert);
                            break;
                        default:
                            throw new Exception("Mode '" + arg1 + "' is not supported.");
                    }
                    break;
                case Actions.IsPowered:
                    // Get info if ULO is powered by electricity from plug
                    output = Convert.ToString(ulo.isPowered());
                    break;
                case Actions.GetBattery:
                    // Get battery capacity
                    output = Convert.ToString(ulo.getBattery());
                    break;
                case Actions.IsCard:
                    // Get info if SD card is inserted into ULO
                    output = Convert.ToString(ulo.isCard());
                    break;
                case Actions.GetCardSpace:
                    // Get SD card free capacity
                    output = Convert.ToString(ulo.getCardSpace());
                    break;
                case Actions.GetDiskSpace:
                    // Get internal memory free capacity
                    output = Convert.ToString(ulo.getDiskSpace());
                    break;
                case Actions.MoveToCard:
                    // Move files from internal memory to SD card
                    ulo.moveToCard();
                    break;
                case Actions.CleanDiskSpace:
                    // Clean files on internal memory
                    string period = String.Empty;
                    switch (arg1.ToLower())
                    {
                        case DeletePeriodMap.OldestDay:
                            period = ULO.DeletePeriod.OldestDay;
                            break;
                        case DeletePeriodMap.OldestWeek:
                            period = ULO.DeletePeriod.OldestWeek;
                            break;
                        case DeletePeriodMap.OldestYear:
                            period = ULO.DeletePeriod.OldestYear;
                            break;
                        case DeletePeriodMap.LatestDay:
                            period = ULO.DeletePeriod.LatestDay;
                            break;
                        case DeletePeriodMap.LatestWeek:
                            period = ULO.DeletePeriod.LatestWeek;
                            break;
                        case DeletePeriodMap.LatestYear:
                            period = ULO.DeletePeriod.LatestYear;
                            break;
                        case DeletePeriodMap.All:
                            period = ULO.DeletePeriod.All;
                            break;
                        default:
                            throw new Exception("Delete period '" + arg1 + "' is not supported.");
                    }
                    ulo.cleanDiskSpace(period);
                    break;
                case Actions.DownloadLog:
                    // Download ULO log into specified location
                    ulo.downloadLog(arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), arg4, arg5);
                    break;
                case Actions.CurrentSnapshot:
                    // Download current snapshot
                    ulo.downloadCurrent(arg1, arg2, arg3, arg4);
                    break;
                case Actions.DownloadVideos:
                    // Download videos
                    ulo.downloadMedia(ULO.MediaType.Video, arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), ((arg4 != String.Empty) ? Int32.Parse(arg4) : 0), arg5, arg6);
                    break;
                case Actions.DownloadSnapshots:
                    // Download snapshots
                    ulo.downloadMedia(ULO.MediaType.Snapshot, arg1, arg2, ((arg3 != String.Empty) ? Int32.Parse(arg3) : 0), ((arg4 != String.Empty) ? Int32.Parse(arg4) : 0), arg5, arg6);
                    break;
                case Actions.TestAvailability:
                    // Test for device availability
                    ulo.testAvailability(arg1);
                    break;
                case Actions.CheckAvailability:
                    // Check for device availability and set proper mode
                    string mode_if_true = "";
                    switch (arg1.ToLower())
                    {
                        case ULO.CameraMode.Standard:
                            mode_if_true = ULO.CameraMode.Standard;
                            break;
                        case ULO.CameraMode.Spy:
                            mode_if_true = ULO.CameraMode.Spy;
                            break;
                        case ULO.CameraMode.Alert:
                            mode_if_true = ULO.CameraMode.Alert;
                            break;
                        default:
                            throw new Exception("Mode '" + arg1 + "' is not supported.");
                    }

                    string mode_if_false = "";
                    switch (arg2.ToLower())
                    {
                        case ULO.CameraMode.Standard:
                            mode_if_false = ULO.CameraMode.Standard;
                            break;
                        case ULO.CameraMode.Spy:
                            mode_if_false = ULO.CameraMode.Spy;
                            break;
                        case ULO.CameraMode.Alert:
                            mode_if_false = ULO.CameraMode.Alert;
                            break;
                        default:
                            throw new Exception("Mode '" + arg2 + "' is not supported.");
                    }
                    
                    string operation = "";
                    switch (arg3.ToLower())
                    {
                        case ULO.Operation.And:
                            operation = ULO.Operation.And;
                            break;
                        case ULO.Operation.Or:
                            operation = ULO.Operation.Or;
                            break;
                        default:
                            throw new Exception("Operation '" + arg3 + "' is not supported.");
                    }

                    ulo.checkAvailability(mode_if_true, mode_if_false, operation, arg4, arg5, arg6, arg7, arg8);
                    break;
                default:
                    throw new Exception("Action '" + action + "' is not supported.");
            }
            
            return output;
        }

        private static void exceptionHandler(Exception ex)
        {
            string timestamp = DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]");
            string errorOutput = String.Empty;
            errorOutput += timestamp + Environment.NewLine;
            errorOutput += "HelpLink   = " + ex.HelpLink + Environment.NewLine;
            errorOutput += "Message    = " + ex.Message + Environment.NewLine;
            errorOutput += "Source     = " + ex.Source + Environment.NewLine;
            errorOutput += "StackTrace = " + ex.StackTrace + Environment.NewLine;
            errorOutput += "TargetSite = " + ex.TargetSite + Environment.NewLine;

            if (ulo.configuration.showTrace)
            {
                Console.WriteLine(String.Empty);
                Console.WriteLine(errorOutput);
            }

            ulo.writeLog(ULO.tempErrFile, errorOutput, false, true);
            ulo.writeLog(ULO.tempOutFile, timestamp + " ERROR: " + ex.Message, false, true);

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
