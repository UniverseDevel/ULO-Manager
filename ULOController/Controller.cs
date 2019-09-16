using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ULOControls;

namespace ULOController
{
    static class Controller
    {
        private static string product_location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static string product_title = ((AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0]).Title;
        private static string product_version = Assembly.GetEntryAssembly().GetName().Version.ToString();
        private static string product_filename = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

        private static string _action = String.Empty;

        private static ULO ulo = new ULO();
        
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

        static void usage()
        {
            /* Length limit - mind the variables, their values and escape symbols */
            /*                  |------------------------------------------------------------------------------| */
            Console.WriteLine(product_title + @" v" + product_version);
            Console.WriteLine(@"");
            Console.WriteLine(@"Usage:");
            Console.WriteLine(@"   ./" + product_filename + @" <ulo_host> <username> <password> <action> <arg1> <argN>");
            Console.WriteLine(@"");
            Console.WriteLine(@"Actions:");
            Console.WriteLine(@"   " + Actions.GetMode + @" - Get current ULO camera mode");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.SetMode + @" - Set ULO camera mode");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. mode - camera recording mode");
            Console.WriteLine(@"               a) " + ULO.CameraMode.Standard + @" - ULO awake and not recording");
            Console.WriteLine(@"               b) " + ULO.CameraMode.Spy + @" - ULO awake and recording");
            Console.WriteLine(@"               c) " + ULO.CameraMode.Alert + @" - ULO asleep and recording");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.IsPowered + @" - Get info if ULO is powered by electricity from plug");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.GetBattery + @" - Get battery capacity");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.IsCard + @" - Get info if SD card is inserted into ULO");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.GetCardSpace + @" - Get SD card free capacity");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.GetDiskSpace + @" - Get internal memory free capacity");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.MoveToCard + @" - Move files from internal memory to SD card");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           None");
            Console.WriteLine(@"                  NOTE: ULO cannot record during this activity.");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.CleanDiskSpace + @" - Clean files on internal memory");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. period - how old/new files should be deleted");
            Console.WriteLine(@"                       NOTE: This action requires admin account and ULO cannot");
            Console.WriteLine(@"                             record during this activity.");
            Console.WriteLine(@"               a) " + DeletePeriodMap.OldestDay + @" - Oldest day");
            Console.WriteLine(@"               b) " + DeletePeriodMap.OldestWeek + @" - Oldest week");
            Console.WriteLine(@"               c) " + DeletePeriodMap.OldestYear + @" - Oldest year");
            Console.WriteLine(@"               d) " + DeletePeriodMap.LatestDay + @" - Latest day");
            Console.WriteLine(@"               e) " + DeletePeriodMap.LatestWeek + @" - Latest week");
            Console.WriteLine(@"               f) " + DeletePeriodMap.LatestYear + @" - Latest year");
            Console.WriteLine(@"               g) " + DeletePeriodMap.All + @" - All");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.DownloadLog + @" - Download ULO log into specified location");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP);
            Console.WriteLine(@"           2. destination path - location where snapshot files should be moved");
            Console.WriteLine(@"                                 NOTE: Alwayse use absolute paths! Destination");
            Console.WriteLine(@"                                       folder must already exist!");
            Console.WriteLine(@"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""");
            Console.WriteLine(@"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)");
            Console.WriteLine(@"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username,");
            Console.WriteLine(@"                        password)");
            Console.WriteLine(@"           3. retention - how old uploaded files should be removed in hours;");
            Console.WriteLine(@"                          if set to 0, no age limit will be used and all");
            Console.WriteLine(@"                          files will be kept");
            Console.WriteLine(@"           4. username");
            Console.WriteLine(@"           5. password");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.CurrentSnapshot + @" - Download current snapshot seen by ULO into specified");
            Console.WriteLine(@"                     location, if snapshot with same name exists it");
            Console.WriteLine(@"                     is overwritten");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP);
            Console.WriteLine(@"           2. destination path - location where snapshot files should be moved");
            Console.WriteLine(@"                                 NOTE: Alwayse use absolute paths! Destination");
            Console.WriteLine(@"                                       folder must already exist!");
            Console.WriteLine(@"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""");
            Console.WriteLine(@"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)");
            Console.WriteLine(@"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username,");
            Console.WriteLine(@"                        password)");
            Console.WriteLine(@"           3. username");
            Console.WriteLine(@"           4. password");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.DownloadVideos + @" - Download all available videos stored in ULO into specified");
            Console.WriteLine(@"                    location, if video with same name exists it is skipped");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP);
            Console.WriteLine(@"           2. destination path - location where video files should be moved");
            Console.WriteLine(@"                                 NOTE: Alwayse use absolute paths! Destination");
            Console.WriteLine(@"                                       folder must already exist!");
            Console.WriteLine(@"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""");
            Console.WriteLine(@"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)");
            Console.WriteLine(@"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username,");
            Console.WriteLine(@"                        password)");
            Console.WriteLine(@"           3. age - how old files should be downloaded in hours; if set");
            Console.WriteLine(@"                    to 0, no age limit will be used and all files will");
            Console.WriteLine(@"                    be downloaded");
            Console.WriteLine(@"           4. retention - how old uploaded files should be removed in hours;");
            Console.WriteLine(@"                          if set to 0, no age limit will be used and all");
            Console.WriteLine(@"                          files will be kept");
            Console.WriteLine(@"           4. username");
            Console.WriteLine(@"           5. password");
            Console.WriteLine(@"");
            Console.WriteLine(@"   " + Actions.DownloadSnapshots + @" - Download all available snapshots stored in ULO into");
            Console.WriteLine(@"                       specified location, if snapshot with same name exists it");
            Console.WriteLine(@"                       is skipped");
            Console.WriteLine(@"       Arguments:");
            Console.WriteLine(@"           1. destination type - " + ULO.DestinationType.Local + @", " + ULO.DestinationType.NFS + @", " + ULO.DestinationType.FTP);
            Console.WriteLine(@"           2. destination path - location where snapshot files should be moved");
            Console.WriteLine(@"                                 NOTE: Alwayse use absolute paths! Destination");
            Console.WriteLine(@"                                       folder must already exist!");
            Console.WriteLine(@"               a) " + ULO.DestinationType.Local + @" - ""<drive>:\<path>\""");
            Console.WriteLine(@"               b) " + ULO.DestinationType.NFS + @" - ""\\<host>\<path>"" (Required: username, password)");
            Console.WriteLine(@"               c) " + ULO.DestinationType.FTP + @" - ""ftp://<host>:<port>/<path>"" (Required: username,");
            Console.WriteLine(@"                        password)");
            Console.WriteLine(@"           3. age - how old files should be downloaded in hours; if set");
            Console.WriteLine(@"                    to 0, no age limit will be used and all files will");
            Console.WriteLine(@"                    be downloaded");
            Console.WriteLine(@"           4. retention - how old uploaded files should be removed in hours;");
            Console.WriteLine(@"                          if set to 0, no age limit will be used and all");
            Console.WriteLine(@"                          files will be kept");
            Console.WriteLine(@"           4. username");
            Console.WriteLine(@"           5. password");
            Console.WriteLine(@"");
            Console.WriteLine(@"Examples:");
            Console.WriteLine(@"    - Download video files");
            Console.WriteLine(@"        ./" + product_filename + @" ""192.168.0.10"" ""test"" ""123!Abc"" ""downloadvideos""");
            Console.WriteLine(@"         ""local"" ""C:\ulo\"" ""24"" ""48""");
            Console.WriteLine(@"");
            Console.WriteLine(@"Library configuration (optional):");
            Console.WriteLine(@"   By creating a text file with name """ + Path.GetFileName(ULO.confFile) + @""" in same directory as ULO");
            Console.WriteLine(@"   library, you can change some library behavior or enable debug options. Each");
            Console.WriteLine(@"   parameter can be set to either true or false, there can be only one parameter");
            Console.WriteLine(@"   per line and there should be equal sign (=) between parameter name and value.");
            Console.WriteLine(@"   Default values are false.");
            Console.WriteLine(@"       1. " + ULO.ConfigParams.WriteLog + @" - write output into log file");
            Console.WriteLine(@"       2. " + ULO.ConfigParams.ShowArguments + @" - incoming arguments will be written to console");
            Console.WriteLine(@"       3. " + ULO.ConfigParams.ShowTrace + @" - error trace will be written to console");
            Console.WriteLine(@"       4. " + ULO.ConfigParams.ShowSkipped + @" - skipped files will be written to log and console");
            Console.WriteLine(@"       5. " + ULO.ConfigParams.SuppressLogHandling + @" - log handler will stop chronologically push logs");
            Console.WriteLine(@"                                into single log file");
            Console.WriteLine(@"");
            Console.WriteLine(@"Examples:");
            Console.WriteLine(@"    " + ULO.ConfigParams.WriteLog + @"=true");
            Console.WriteLine(@"    " + ULO.ConfigParams.ShowArguments + @"=false");
            Console.WriteLine(@"");
            Console.WriteLine(@"Notes from working with ULO:");
            Console.WriteLine(@"    - When using this tool, ULO usualy wakes up unless it is in Alert mode.");
            Console.WriteLine(@"    - Transfer speeds usualy depends on WiFi signal strength or ULOs");
            Console.WriteLine(@"      processing power. Due to way how we access files there is not much space");
            Console.WriteLine(@"      to make this process faster in this code.");
            Console.WriteLine(@"    - Files from ULO memory can be emptied only in standard mode.");
            Console.WriteLine(@"    - This tool properly logs in and logs out into ULO; because of this,");
            Console.WriteLine(@"      if you use same user for browser access this user will be logged");
            Console.WriteLine(@"      out along with this tool at the end of execution.");
            Console.WriteLine(@"    - It is advised to create new user without admin privileges to use this");
            Console.WriteLine(@"      tool, unless you need to perform tasks that require them. For now");
            Console.WriteLine(@"      it seems that ULO can create mutiple users, but they sometimes have");
            Console.WriteLine(@"      problems to log in.");
            Console.WriteLine(@"    - If mutiple activities are performed at a same time or their execution");
            Console.WriteLine(@"      might overlap, it is advised to create separate ULO users for such");
            Console.WriteLine(@"      activities.");
            Console.WriteLine(@"    - NFS cannot be both used in Windows and used by script, if used so,");
            Console.WriteLine(@"      one or the other might stop working after some time.");
            Console.WriteLine(@"    - FTP upload supports anonymouse login.");
            Console.WriteLine(@"    - FTP is very permission sensitive, wrongly set permissions may lead to");
            Console.WriteLine(@"      some features returning errors.");
            Console.WriteLine(@"    - ULO can perform unintended self reeboots which always reset current");
            Console.WriteLine(@"      camera mode to standard and therefore ULO will stop recodring.");
            /*                  |------------------------------------------------------------------------------| */
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
                                    if (index == 8) { revised_output = password_stars; }
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
                if (host == "/?" || host == String.Empty || host == "?" || host == "-help" || host == "--help")
                {
                    usage();
                }
                else
                {
                    try
                    {
                        // Login
                        ulo.login(host, username, password);

                        // Perform action
                        switch (action.ToLower())
                        {
                            // Developement actions
                            case Actions.CallAPI:
                                // Get API output
                                ulo.writeLog(ULO.tempOutFile, ulo.callAPI(arg1, arg2, arg3, arg4), true);
                                break;
                            // User actions
                            case Actions.GetMode:
                                // Get mode
                                ulo.writeLog(ULO.tempOutFile, ulo.getMode(), true);
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
                                ulo.writeLog(ULO.tempOutFile, Convert.ToString(ulo.isPowered()), true);
                                break;
                            case Actions.GetBattery:
                                // Get battery capacity
                                ulo.writeLog(ULO.tempOutFile, Convert.ToString(ulo.getBattery()), true);
                                break;
                            case Actions.IsCard:
                                // Get info if SD card is inserted into ULO
                                ulo.writeLog(ULO.tempOutFile, Convert.ToString(ulo.isCard()), true);
                                break;
                            case Actions.GetCardSpace:
                                // Get SD card free capacity
                                ulo.writeLog(ULO.tempOutFile, Convert.ToString(ulo.getCardSpace()), true);
                                break;
                            case Actions.GetDiskSpace:
                                // Get internal memory free capacity
                                ulo.writeLog(ULO.tempOutFile, Convert.ToString(ulo.getDiskSpace()), true);
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
                            default:
                                throw new Exception("Action '" + action + "' is not supported.");
                        }

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

        private static void exceptionHandler(Exception ex)
        {
            string errorOutput = String.Empty;
            errorOutput += DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine;
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

            //throw ex;
        }
    }
}
