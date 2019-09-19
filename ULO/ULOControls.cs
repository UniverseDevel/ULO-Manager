using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ULOControls
{
    public class ULO
    {
        /*
        If you want to enjoy a little fun by ULOs India developement 
        team, log in to your ULO and then insert following URL 
        into your browser: http://<ULO_IP>/assets/sounds/snapshot_20170829_092122.mp4
        it's either test file or they were not so hard at work.
        Confirmed in version 01.0101 trough 10.1308.

        Be careful when using ULO: https://support.ulo.camera/hc/en-us/community/posts/360005096479-ULO-security-risk-for-your-network-and-worse
        */

        // List of supported version that this script was tested on, not all versions were catched
        private static string[] supportedVersions = new string[] { "01.0101", "08.0803", "08.0804", "08.0904", "10.1308" };

        // Private
        private static readonly string product_location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private string host = String.Empty;
        private string token = String.Empty;

        // Public
        public static readonly int processId = Process.GetCurrentProcess().Id;
        public static readonly string filesName = "ULOControls";
        public static readonly string timeFormat = "yyyyMMdd_HHmmss";
        public static readonly string filesTimestamp = DateTime.Now.ToString(timeFormat);
        public static readonly string tempOutFile = product_location + "\\" + filesName + "_" + filesTimestamp + "_" + processId.ToString() + ".out.tmp";
        public static readonly string tempErrFile = product_location + "\\" + filesName + "_" + filesTimestamp + "_" + processId.ToString() + ".err.tmp";
        public static readonly string outFile = product_location + "\\" + filesName + ".out";
        public static readonly string errFile = product_location + "\\" + filesName + ".err";
        public static readonly string confFile = product_location + "\\" + filesName + ".conf";
        public static readonly string archivePath = product_location + "\\archive";
        public static readonly string logHandlerFlag = product_location + "\\LOG_HANDLING";
        public DateTime session_start = DateTime.Now;
        public DateTime session_end = DateTime.Now;
        public bool is_supported = false;
        public string currentVersion = String.Empty;
        public int ping_timeout = 5000;

        public Configuration configuration = new Configuration();

        private void clear()
        {
            //response = String.Empty;
            host = String.Empty;
            token = String.Empty;
            session_start = DateTime.Now;
            session_end = DateTime.Now;
            is_supported = false;
            currentVersion = String.Empty;
        }

        public class ConfigParams
        {
            public const string WriteLog = "writeLog";
            public const string ShowArguments = "showArguments";
            public const string ShowTrace = "showTrace";
            public const string ShowSkipped = "showSkipped";
            public const string ShowPingResults = "showPingResults";
            public const string SuppressLogHandling = "suppressLogHandling";
        }

        public class Configuration
        {
            public bool writeLog = readConfigBool(ConfigParams.WriteLog, false);
            public bool showArguments = readConfigBool(ConfigParams.ShowArguments, false);
            public bool showTrace = readConfigBool(ConfigParams.ShowTrace, false);
            public bool showSkipped = readConfigBool(ConfigParams.ShowSkipped, false);
            public bool ShowPingResults = readConfigBool(ConfigParams.ShowPingResults, false);
            public bool suppressLogHandling = readConfigBool(ConfigParams.SuppressLogHandling, false);
        }

        public class CameraMode
        {
            public const string Standard = "standard";
            public const string Spy = "spy";
            public const string Alert = "alert";
        }

        public class DeletePeriod
        {
            public const string OldestDay = "0";
            public const string OldestWeek = "1";
            public const string OldestYear = "2";
            public const string LatestDay = "3";
            public const string LatestWeek = "4";
            public const string LatestYear = "5";
            public const string All = "6";
        }

        public class DestinationType
        {
            public const string Local = "local";
            public const string NFS = "nfs";
            public const string FTP = "ftp";
        }

        public class Operation
        {
            public const string And = "and";
            public const string Or = "or";
        }

        private class FsAction
        {
            public const string Upload = "upload";
            public const string Retention = "retention";
        }

        public class MediaType
        {
            public const string Video = "video";
            public const string Snapshot = "snapshot";
        }

        public class MediaTypeExt
        {
            public const string Video = "mp4";
            public const string Snapshot = "jpg";
            public const string Log = "zip";
        }

        private class UploadStatistics
        {
            public long fileSize = 0;
            public double downloadTime = 0;
            public double uploadTime = 0;
            public int skipped = 0;
            public int overwritten = 0;
            public int succeeded = 0;
            public int failed = 0;
            public int removed = 0;
        }

        public ULO()
        {
            // Init
        }

        public void reloadConfiguration()
        {
            configuration = new Configuration();
        }

        public bool ping(string host, bool output)
        {
            if (!host.Contains("://"))
            {
                host = "https://" + host;
            }
            host = new Uri(host).Host;

            Ping ping = new Ping();

            string data = new String('.', 32);
            int packet_size = Encoding.UTF8.GetByteCount(data);
            byte[] buffer = Encoding.UTF8.GetBytes(data);

            PingOptions options = new PingOptions(64, true);
            PingReply reply = ping.Send(host, ping_timeout, buffer, options);

            bool state = false;

            if (reply.Status == IPStatus.Success)
            {
                if (output)
                {
                    writeLog(tempOutFile, "Address:             " + reply.Address.ToString(), true);
                    writeLog(tempOutFile, "RoundTrip time [ms]: " + reply.RoundtripTime, true);
                    writeLog(tempOutFile, "Time to live [hops]: " + reply.Options.Ttl, true);
                    writeLog(tempOutFile, "Don't fragment:      " + reply.Options.DontFragment, true);
                    writeLog(tempOutFile, "Buffer size [bytes]: " + reply.Buffer.Length, true);
                }

                state = true;
            }
            else
            {
                if (output)
                {
                    writeLog(tempOutFile, "Ping failed with status: " + reply.Status, true);
                }

                state = false;
            }

            return state;
        }

        public void testAvailability(string host)
        {
            // Test for device availability
            if (ping(host, true))
            {
                writeLog(tempOutFile, "", true);
                writeLog(tempOutFile, "Device is available.", true);
            }
            else
            {
                writeLog(tempOutFile, "", true);
                writeLog(tempOutFile, "Device is NOT available.", true);
            }
        }

        public void checkAvailability(string if_true, string if_false, string operation, string host1, string host2, string host3, string host4, string host5)
        {
            // Check for device availability and set proper mode
            writeLog(tempOutFile, "", true);
            writeLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);

            string[] hosts = new string[] { };
            bool result = false;
            bool result_step = false;

            switch (operation)
            {
                case Operation.And:
                    result_step = true;
                    break;
                case Operation.Or:
                    result_step = false;
                    break;
                default:
                    throw new Exception("Operation '" + operation + "' is not supported.");
            }

            if (host1 != String.Empty)
            {
                Array.Resize(ref hosts, hosts.Length + 1);
                hosts[hosts.Length - 1] = host1;
            }
            if (host2 != String.Empty)
            {
                Array.Resize(ref hosts, hosts.Length + 1);
                hosts[hosts.Length - 1] = host2;
            }
            if (host3 != String.Empty)
            {
                Array.Resize(ref hosts, hosts.Length + 1);
                hosts[hosts.Length - 1] = host3;
            }
            if (host4 != String.Empty)
            {
                Array.Resize(ref hosts, hosts.Length + 1);
                hosts[hosts.Length - 1] = host4;
            }
            if (host5 != String.Empty)
            {
                Array.Resize(ref hosts, hosts.Length + 1);
                hosts[hosts.Length - 1] = host5;
            }

            foreach (string host in hosts)
            {
                result = ping(host, false);

                if (configuration.ShowPingResults)
                {
                    writeLog(tempOutFile, "Host:                    " + host, true);
                    writeLog(tempOutFile, "Operation:               " + operation, true);
                    writeLog(tempOutFile, "Result:                  " + result, true);
                    writeLog(tempOutFile, "Result step before eval: " + result_step, true);
                }

                switch (operation)
                {
                    case Operation.And:
                        if (result && result_step) { result_step = true; } else { result_step = false; }
                        break;
                    case Operation.Or:
                        if (result || result_step) { result_step = true; } else { result_step = false; }
                        break;
                    default:
                        throw new Exception("Operation '" + operation + "' is not supported.");
                }

                if (configuration.ShowPingResults)
                {
                    writeLog(tempOutFile, "Result step after eval:  " + result_step, true);
                    writeLog(tempOutFile, "===============================================", true);
                }
            }

            if (result_step)
            {
                switch (operation)
                {
                    case Operation.And:
                        writeLog(tempOutFile, "All devices are available.", true);
                        break;
                    case Operation.Or:
                        writeLog(tempOutFile, "At least one device is available.", true);
                        break;
                    default:
                        throw new Exception("Operation '" + operation + "' is not supported.");
                }

                // Set mode based on provided value
                writeLog(tempOutFile, "Setting camera to '" + if_true + "' mode.", true);
                setMode(if_true);
            }
            else
            {
                switch (operation)
                {
                    case Operation.And:
                        writeLog(tempOutFile, "At least one device is not available.", true);
                        break;
                    case Operation.Or:
                        writeLog(tempOutFile, "All devices are not available.", true);
                        break;
                    default:
                        throw new Exception("Operation '" + operation + "' is not supported.");
                }

                // Set mode based on provided value
                writeLog(tempOutFile, "Setting camera to '" + if_false + "' mode.", true);
                setMode(if_false);
            }
        }

        public void login(string ulo_host, string username, string password)
        {
            // Workaround for invalid certificate
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Login
            if (!ulo_host.Contains("://"))
            {
                ulo_host = "https://" + ulo_host;
            }
            host = "https://" + new Uri(ulo_host).Host;
            string response = httpCall(host + "/api/v1/login", "POST", "{ \"iOSAgent\": false }", BasicAuth(username, password));
            session_start = DateTime.Now;
            token = getJsonString(response, "token");
            session_end = session_start.AddSeconds(getJsonInt(response, "expiresIn"));

            // Check version support
            is_supported = checkVersion();
        }

        public void logout()
        {
            // Logout
            if (token != String.Empty)
            {
                callAPI("/api/v1/logout", "POST", "{}", String.Empty);
            }

            clear();
        }

        public DateTime getULOTime()
        {
            // Get time
            DateTime ulo_time = Convert.ToDateTime(callAPI("/api/v1/time", "GET", String.Empty, "time"));
            if (ulo_time == new DateTime(1970, 1, 1, 0, 0, 0))
            {
                ulo_time = DateTime.Now;
            }

            return ulo_time;
        }

        public bool checkVersion()
        {
            // Check version
            bool supported = false;
            currentVersion = callAPI("/api/v1/config", "GET", String.Empty, "firmware.currentversion");
            foreach (string supportedVersion in supportedVersions)
            {
                if (supportedVersion == currentVersion)
                {
                    supported = true;
                }
            }
            if (!supported)
            {
                writeLog(tempOutFile, "WARNING: Your current ULO version (" + currentVersion + ") is not tested, yet. Results may vary.", true);
            }

            return supported;
        }

        public string getMode()
        {
            // Get mode
            return callAPI("/api/v1/mode", "GET", String.Empty, "mode");
        }

        public void setMode(string mode)
        {
            // Set mode
            if (callAPI("/api/v1/mode", "PUT", "{ \"mode\": \"" + mode + "\" }", "mode").ToLower() != mode)
            {
                throw new Exception("Mode change failed.");
            }
            else
            {
                writeLog(tempOutFile, "Success.", true);
            }
        }

        public bool isPowered()
        {
            // Get info if ULO is powered by electricity from plug
            return Convert.ToBoolean(callAPI("/api/v1/state", "GET", String.Empty, "plugged"));
        }

        public int getBattery()
        {
            // Get battery capacity
            return Convert.ToInt32(callAPI("/api/v1/state", "GET", String.Empty, "batteryLevel"));
        }

        public bool isCard()
        {
            // Get info if SD card is inserted into ULO
            return Convert.ToBoolean(callAPI("/api/v1/files/stats", "GET", String.Empty, "sdcard.inserted"));
        }

        public int getCardSpace()
        {
            // Get SD card free capacity
            return Convert.ToInt32(callAPI("/api/v1/files/stats", "GET", String.Empty, "sdcard.freeMB"));
        }

        public int getDiskSpace()
        {
            // Get internal memory free capacity
            return Convert.ToInt32(callAPI("/api/v1/files/stats", "GET", String.Empty, "internal.freeMB"));
        }

        public void moveToCard()
        {
            // Move files from internal memory to SD card
            string mode_backup = getMode();
            setMode(CameraMode.Standard);
            string response = callAPI("/api/v1/files/backup?filename=all", "PUT", "{\"running\": true}", "$");
            setMode(mode_backup);
            
            string error = getJsonString(response, "error");
            if (!String.IsNullOrEmpty(error))
            {
                throw new Exception(error);
            }
            else
            {
                string status = getJsonString(response, "status");
                writeLog(tempOutFile, status, true);
            }
        }

        public void cleanDiskSpace(string period)
        {
            // Clean files on internal memory
            string mode_backup = getMode();
            setMode(CameraMode.Standard);
            string response = callAPI("/api/v1/files/delete?removeType=" + period, "DELETE", String.Empty, "$");
            setMode(mode_backup);

            string error = getJsonString(response, "error");
            if (error != String.Empty)
            {
                throw new Exception(error);
            }
            else
            {
                string status = getJsonString(response, "status");
                writeLog(tempOutFile, status, true);
            }
        }

        public void downloadLog(string type, string destination, int retention, string username, string password)
        {
            // Download ULO log into specified location
            string log_location = callAPI("/api/v1/system/log", "POST", "/system/log", "fileName").Replace("\n", Environment.NewLine);
            UploadStatistics logStats = new UploadStatistics();
            logStats = uploadHandler(type, "/logs/" + log_location, destination, false, username, password);
            logStats = retentionHandler(type, destination, retention, MediaTypeExt.Log, username, password);
        }

        public void downloadCurrent(string type, string destination, string username, string password)
        {
            // Download current snapshot
            UploadStatistics fileStats = uploadHandler(type, "/" + callAPI("/api/v1/backgroundImage", "POST", "{}", "filename"), destination, true, username, password);
        }

        public void downloadMedia(string mediatype, string type, string destination, int age, int retention, string username, string password)
        {
            // Download all media requested and maintain them
            writeLog(tempOutFile, "", true);
            writeLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);

            Regex regex = null;
            Match match = null;
            string[] index = new string[] { };
            string[] folders = new string[] { };
            string[] mediafiles = new string[] { };
            string mediatype_extension = null;

            // Set values based on mediatype
            switch (mediatype)
            {
                case MediaType.Video:
                    mediatype_extension = MediaTypeExt.Video;
                    break;
                case MediaType.Snapshot:
                    mediatype_extension = MediaTypeExt.Snapshot;
                    break;
                default:
                    throw new Exception("Media type '" + mediatype + "' is not supported.");
            }

            // Get list of folders
            string response = httpCall(host + "/media/", "GET", String.Empty, BearerAuth(token));
            index = response.Split(new string[] { "<a " }, StringSplitOptions.None);
            regex = new Regex(@"(\/media\/\d+/)");
            foreach (string part in index)
            {
                match = regex.Match(part);
                if (match.Success)
                {
                    Array.Resize(ref folders, folders.Length + 1);
                    folders[folders.Length - 1] = match.Value;
                }
            }

            // Get list of media files
            foreach (string folder in folders)
            {
                response = httpCall(host + folder, "GET", String.Empty, BearerAuth(token));
                index = response.Split(new string[] { "<a " }, StringSplitOptions.None);
                regex = new Regex(@"(\/media\/\d+/video_\d+_\d+." + mediatype_extension + ")");
                foreach (string part in index)
                {
                    match = regex.Match(part);
                    if (match.Success)
                    {
                        Array.Resize(ref mediafiles, mediafiles.Length + 1);
                        mediafiles[mediafiles.Length - 1] = match.Value;
                    }
                }
            }

            // Download media files
            regex = new Regex(@"(\d+_\d+)");
            DateTime ulo_time = getULOTime();
            DateTime age_time = ulo_time.AddHours(age * -1);
            DateTime fresh_file_time = ulo_time.AddMinutes(-1);
            UploadStatistics totalStats = new UploadStatistics();
            bool stop_processing = false;
            foreach (string mediafile in mediafiles)
            {
                UploadStatistics fileStats = new UploadStatistics();

                try
                {
                    // Check age
                    string mediafilename = Path.GetFileName(mediafile.Replace("/", "\\"));
                    DateTime mediafile_time = DateTime.ParseExact(regex.Match(mediafilename).Value, timeFormat, null);

                    if (age != 0 && mediafile_time < age_time)
                    {
                        fileStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            writeLog(tempOutFile, "Media file '" + mediafile + "' is too old based on age settings...", true);
                            writeLog(tempOutFile, "Skipped.", true);
                        }
                    }
                    else
                    {
                        // Check if last media file is old enough to be downloaded
                        if (mediafiles[mediafiles.Length - 1] == mediafile && mediafile_time > fresh_file_time)
                        {
                            fileStats.skipped = 1;
                            writeLog(tempOutFile, "Media file '" + mediafile + "' might be still used by ULO, it will be downloaded later...", true);
                            writeLog(tempOutFile, "Skipped.", true);
                        }
                        else
                        {
                            try
                            {
                                fileStats = uploadHandler(type, mediafile, destination, false, username, password);
                            }
                            catch (Exception ex)
                            {
                                stop_processing = false;
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (stop_processing)
                    {
                        throw new Exception(ex.Message);
                    }
                    fileStats.failed = 1;
                    writeLog(tempOutFile, "Failed. (Error: " + ex.Message + ")", true);
                }

                totalStats.fileSize = totalStats.fileSize + fileStats.fileSize;
                totalStats.downloadTime = totalStats.downloadTime + fileStats.downloadTime;
                totalStats.uploadTime = totalStats.uploadTime + fileStats.uploadTime;
                totalStats.skipped = totalStats.skipped + fileStats.skipped;
                totalStats.overwritten = totalStats.overwritten + fileStats.overwritten;
                totalStats.succeeded = totalStats.succeeded + fileStats.succeeded;
                totalStats.failed = totalStats.failed + fileStats.failed;
            }


            // Perform clean-up based on retention
            if (retention != 0)
            {
                writeLog(tempOutFile, "Retention clean-up...", true);

                // Perform clean-up based on retention
                UploadStatistics retentionStats = new UploadStatistics();

                try
                {
                    retentionStats = retentionHandler(type, destination, retention, mediatype_extension, username, password);
                }
                catch (Exception ex)
                {
                    stop_processing = false;
                    throw;
                }

                totalStats.removed = totalStats.removed + retentionStats.removed;
            }

            // Output summary
            writeLog(tempOutFile, "=====================================", true);
            writeLog(tempOutFile, "                DONE                 ", true);
            writeLog(tempOutFile, "=====================================", true);
            writeLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);
            writeLog(tempOutFile, "Stats:", true);
            writeLog(tempOutFile, "     Media type:        " + mediatype, true);
            writeLog(tempOutFile, "     Files uploaded:    " + totalStats.succeeded, true);
            writeLog(tempOutFile, "     Files failed:      " + totalStats.failed, true);
            writeLog(tempOutFile, "     Files skipped:     " + totalStats.skipped, true);
            writeLog(tempOutFile, "     Files overwritten: " + totalStats.overwritten, true);
            writeLog(tempOutFile, "     Files removed:     " + totalStats.removed, true);
            writeLog(tempOutFile, "     Files size:        " + bytesToString(totalStats.fileSize), true);
            writeLog(tempOutFile, "     Download time:     " + Math.Round(totalStats.downloadTime, 2) + " sec", true);
            writeLog(tempOutFile, "     Upload time:       " + Math.Round(totalStats.uploadTime, 2) + " sec", true);
            writeLog(tempOutFile, "     Down&Up time:      " + Math.Round(totalStats.downloadTime + totalStats.uploadTime, 2) + " sec", true);
            writeLog(tempOutFile, "     Avg. down. speed:  " + bytesToString((long)Math.Round(totalStats.fileSize / (totalStats.downloadTime + 1), 2)) + "/s", true);
            writeLog(tempOutFile, "     Avg. up. speed:    " + bytesToString((long)Math.Round(totalStats.fileSize / (totalStats.uploadTime + 1), 2)) + "/s", true);
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics fsHandler(string type, string fs_action, string source, string destination, int retention, bool overwrite, string mediatype_extension, string username, string password)
        {
            UploadStatistics handlingStats = new UploadStatistics();

            switch (type)
            {
                case DestinationType.Local:
                    switch (fs_action)
                    {
                        case FsAction.Upload:
                            handlingStats = uploadLocal(source, destination, overwrite);
                            break;
                        case FsAction.Retention:
                            handlingStats = retentionLocal(destination, retention, mediatype_extension);
                            break;
                    }
                    break;
                case DestinationType.NFS:
                    // Open connection to NFS
                    NetworkCredential credentials = new NetworkCredential(username, password);
                    using (ConnectToSharedFolder nfsConnection = new ConnectToSharedFolder(destination, credentials))
                    {
                        // NFS is used like Local, just needs connection to NFS upfront
                        switch (fs_action)
                        {
                            case FsAction.Upload:
                                handlingStats = uploadLocal(source, destination, overwrite);
                                break;
                            case FsAction.Retention:
                                handlingStats = retentionLocal(destination, retention, mediatype_extension);
                                break;
                        }
                    }
                    break;
                case DestinationType.FTP:
                    switch (fs_action)
                    {
                        case FsAction.Upload:
                            handlingStats = uploadFTP(source, destination, overwrite, username, password);
                            break;
                        case FsAction.Retention:
                            handlingStats = retentionFTP(destination, retention, mediatype_extension, username, password);
                            break;
                    }
                    break;
                default:
                    throw new Exception("Destination type '" + type + "' is not supported.");
            }

            return handlingStats;
        }

        private UploadStatistics uploadHandler(string type, string source, string destination, bool overwrite, string username, string password)
        {
            return fsHandler(type, FsAction.Upload, source, destination, 0, overwrite, String.Empty, username, password);
        }

        private UploadStatistics retentionHandler(string type, string destination, int retention, string mediatype_extension, string username, string password)
        {
            return fsHandler(type, FsAction.Retention, String.Empty, destination, retention, false, mediatype_extension, username, password);
        }

        private string mediaNameAdjust(string mediafilename)
        {
            // Name conversion is needed so FTP retention has same file format with date and time available
            string mediafilename_adjusted = mediafilename;

            Regex regexSortableISO = new Regex(@"(\d+-\d+-\d+T\d+-\d+-\d+)"); // 2019-09-07T12-20-33
            Regex regexSortable = new Regex(@"(\d+_\d+)"); // 20190907_122033
            Regex regexLogin = new Regex(@"(loginPicture\.jpg)"); // loginPicture.jpg

            if (regexSortableISO.Match(mediafilename).Success)
            {
                DateTime mediafile_time = DateTime.ParseExact(regexSortableISO.Match(mediafilename).Value, "yyyy-MM-ddTHH-mm-ss", null);
                mediafilename_adjusted = regexSortableISO.Replace(mediafilename, mediafile_time.ToString(timeFormat));
            }
            else if (regexSortable.Match(mediafilename).Success)
            {
                DateTime mediafile_time = DateTime.ParseExact(regexSortable.Match(mediafilename).Value, "yyyyMMdd_HHmmss", null);
                mediafilename_adjusted = regexSortable.Replace(mediafilename, mediafile_time.ToString(timeFormat));
            }
            else if (regexLogin.Match(mediafilename).Success)
            {
                mediafilename_adjusted = regexLogin.Replace(mediafilename, "snapshot.jpg");
            }
            
            return mediafilename_adjusted;
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics uploadLocal(string mediafile, string destination, bool overwrite)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            // Create names and paths
            bool is_local = File.Exists(mediafile);
            string mediafilename = mediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
            string mediafile_to_path = mediaNameAdjust(mediafile.Replace("/", "\\"));
            string pathto = mediafile_to_path.Replace(mediafilename, "").Replace("\\media", "").Replace("\\logs", "");
            string full_path = destination.TrimEnd('\\') + pathto + mediafilename;
            string source = String.Empty;

            if (is_local)
            {
                mediafilename = mediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
                mediafile_to_path = String.Empty;
                pathto = "\\";
                full_path = destination.TrimEnd('\\') + pathto + mediafilename;
                source = mediafile.Replace("/", "\\");
            }

            try
            {
                // Check if media file already exists at destination
                if (File.Exists(full_path))
                {
                    if (!overwrite)
                    {
                        uploadStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            writeLog(tempOutFile, "Media file '" + mediafile + "' already downloaded...", true);
                            writeLog(tempOutFile, "Skipped.", true);
                        }
                        return uploadStats;
                    }
                    else
                    {
                        uploadStats.overwritten = 1;
                    }
                }

                writeLog(tempOutFile, "Media file '" + mediafile + "' downloading...", true);

                // Create destination folder
                Directory.CreateDirectory(destination.TrimEnd('\\') + pathto);
                if (File.Exists(full_path))
                {
                    File.Delete(full_path);
                }
                
                // Download media file
                DateTime downloadstart = DateTime.Now;
                if (!is_local)
                {
                    source = downloadFile(mediafile, BearerAuth(token));
                }
                DateTime downloadend = DateTime.Now;
                double downloadtime = (downloadend - downloadstart).TotalSeconds;
                long filesize = new FileInfo(source).Length;
                
                // Upload mediafile
                DateTime uploadstart = DateTime.Now;
                File.Move(source, full_path);
                DateTime uploadend = DateTime.Now;
                double uploadtime = (uploadend - uploadstart).TotalSeconds;

                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                // Colllect stats
                uploadStats.fileSize = filesize;
                uploadStats.downloadTime = downloadtime;
                uploadStats.uploadTime = uploadtime;
                uploadStats.succeeded = 1;

                writeLog(tempOutFile, "Succeeded. (Size: " + bytesToString(filesize) + " | Time: " + Math.Round(downloadtime + uploadtime, 2) + " sec | Download speed: " + bytesToString((long)Math.Round(filesize / (downloadtime + 1), 2)) + "/s | Upload speed: " + bytesToString((long)Math.Round(filesize / (uploadtime + 1), 2)) + "/s)", true);
            }
            catch (Exception ex)
            {
                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                throw;
            }

            // Return stats
            return uploadStats;
        }

        private UploadStatistics retentionLocal(string destination, int retention, string mediatype_extension)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            string[] directories = Directory.GetDirectories(destination);
            Array.Sort(directories);
            foreach (string directory in directories)
            {
                writeLog(tempOutFile, "Retention clean-up of files in directory '" + directory + "' started...", true);

                string[] files = Directory.GetFiles(directory);
                Array.Sort(files);
                foreach (string file in files)
                {
                    if (Path.GetExtension(file).ToLower() != "." + mediatype_extension)
                    {
                        continue;
                    }

                    FileInfo fi = new FileInfo(file);
                    if (fi.CreationTime < DateTime.Now.AddHours(retention * -1))
                    {
                        try
                        {
                            writeLog(tempOutFile, "Retention clean-up of file '" + directory.Replace(destination, "") + "\\" + fi.Name + "' started...", true);
                            fi.Delete();
                            uploadStats.removed++;
                        }
                        catch (Exception ex)
                        {
                            writeLog(tempOutFile, "Retention clean-up of file '" + directory.Replace(destination, "") + "\\" + fi.Name + "' failed due to: " + ex.Message + ".", true);
                        }
                    }
                }

                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    try
                    {
                        writeLog(tempOutFile, "Retention clean-up of direcotry '" + directory + "' started...", true);
                        Directory.Delete(directory, false);
                    }
                    catch (Exception ex)
                    {
                        writeLog(tempOutFile, "Retention clean-up of direcotry '" + directory + "' failed due to: " + ex.Message + ".", true);
                    }
                }
            }
            
            // Return stats
            return uploadStats;
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics uploadFTP(string mediafile, string destination, bool overwrite, string username, string password)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            // Create names and paths
            bool is_local = File.Exists(mediafile);
            string mediafilename = mediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
            string mediafile_to_path = mediaNameAdjust(mediafile);
            string pathto = mediafile_to_path.Replace(mediafilename, "").Replace("/media", "").Replace("/logs", "");
            string full_path = destination.TrimEnd('/') + pathto + mediafilename;
            string source = String.Empty;
            FtpWebRequest request = null;
            FtpWebResponse response = null;
            NetworkCredential cred = new NetworkCredential(username, password);

            if (is_local)
            {
                mediafilename = mediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
                mediafile_to_path = String.Empty;
                pathto = "\\";
                full_path = destination.TrimEnd('\\') + pathto + mediafilename;
                source = mediafile.Replace("/", "\\");
            }

            // Check if media file already exists at destination
            try
            {
                if (existsFTP(full_path, cred))
                {
                    if (!overwrite)
                    {
                        uploadStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            writeLog(tempOutFile, "Media file '" + mediafile + "' already downloaded...", true);
                            writeLog(tempOutFile, "Skipped.", true);
                        }
                        return uploadStats;
                    }
                    else
                    {
                        uploadStats.overwritten = 1;
                    }
                }

                writeLog(tempOutFile, "Media file '" + mediafile + "' downloading...", true);

                // Create destination folder
                // Root
                try
                {
                    request = (FtpWebRequest)WebRequest.Create(destination.TrimEnd('/'));
                    request.Credentials = cred;
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    response = (FtpWebResponse)request.GetResponse();
                    response.Close();
                }
                catch (WebException ex)
                {
                    response = (FtpWebResponse)ex.Response;
                    if (response.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable)
                    {
                        string status = response.StatusDescription.Trim();
                        response.Close();
                        throw new Exception(status);
                    }
                    response.Close();
                }
                // Path
                try
                {
                    request = (FtpWebRequest)WebRequest.Create(destination.TrimEnd('/') + pathto);
                    request.Credentials = cred;
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    response = (FtpWebResponse)request.GetResponse();
                    response.Close();
                }
                catch (WebException ex)
                {
                    response = (FtpWebResponse)ex.Response;
                    if (response.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable)
                    {
                        string status = response.StatusDescription.Trim();
                        response.Close();
                        throw new Exception(status);
                    }
                    response.Close();
                }

                // Download media file
                DateTime downloadstart = DateTime.Now;
                if (!is_local)
                {
                    source = downloadFile(mediafile, BearerAuth(token));
                }
                DateTime downloadend = DateTime.Now;
                double downloadtime = (downloadend - downloadstart).TotalSeconds;
                long filesize = new FileInfo(source).Length;

                // Upload media file
                WebClient client = new WebClient();
                client.Credentials = cred;

                DateTime uploadstart = DateTime.Now;
                client.UploadFile(full_path, WebRequestMethods.Ftp.UploadFile, source);
                DateTime uploadend = DateTime.Now;
                double uploadtime = (uploadend - uploadstart).TotalSeconds;

                client.Dispose();

                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                // Colllect stats
                uploadStats.fileSize = filesize;
                uploadStats.downloadTime = downloadtime;
                uploadStats.uploadTime = uploadtime;
                uploadStats.succeeded = 1;

                writeLog(tempOutFile, "Succeeded. (Size: " + bytesToString(filesize) + " | Time: " + Math.Round(downloadtime + uploadtime, 2) + " sec | Download speed: " + bytesToString((long)Math.Round(filesize / (downloadtime + 1), 2)) + "/s | Upload speed: " + bytesToString((long)Math.Round(filesize / (uploadtime + 1), 2)) + "/s)", true);
            }
            catch (Exception ex)
            {
                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                throw;
            }

            // Return stats
            return uploadStats;
        }

        private UploadStatistics retentionFTP(string destination, int retention, string mediatype_extension, string username, string password)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            NetworkCredential cred = new NetworkCredential(username, password);
            FtpWebRequest request = null;
            FtpWebResponse response = null;
            StreamReader streamReader = null;

            try
            {
                request = (FtpWebRequest)WebRequest.Create(destination.TrimEnd('/'));
                request.Credentials = cred;
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                string[] directories = new string[] { };
                using (response = (FtpWebResponse)request.GetResponse())
                {
                    using (streamReader = new StreamReader(response.GetResponseStream()))
                    {
                        string line = streamReader.ReadLine();
                        while (!string.IsNullOrEmpty(line))
                        {
                            Array.Resize(ref directories, directories.Length + 1);
                            directories[directories.Length - 1] = destination.TrimEnd('/') + "/" + line;
                            line = streamReader.ReadLine();
                        }
                    }
                }

                Array.Sort(directories);
                foreach (string directory in directories)
                {
                    request = (FtpWebRequest)WebRequest.Create(directory.TrimEnd('/'));
                    request.Credentials = cred;
                    request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                    string[] files = new string[] { };
                    int file_count = 0;
                    using (response = (FtpWebResponse)request.GetResponse())
                    {
                        using (streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            string line = streamReader.ReadLine();
                            while (!string.IsNullOrEmpty(line))
                            {
                                string file_name = line.Split(new[] { ' ', '\t' })[line.Split(new[] { ' ', '\t' }).Length - 1];

                                if (file_name == "." || file_name == "..")
                                {
                                    line = streamReader.ReadLine();
                                    continue;
                                }

                                file_count++;

                                if (Path.GetExtension(file_name).ToLower() != "." + mediatype_extension)
                                {
                                    line = streamReader.ReadLine();
                                    continue;
                                }

                                Array.Resize(ref files, files.Length + 1);
                                files[files.Length - 1] = directory.TrimEnd('/') + "/" + file_name;
                                line = streamReader.ReadLine();
                            }
                        }
                    }

                    // Remove files if too old
                    int removed_count = 0;
                    if (files.Length != 0)
                    {
                        Array.Sort(files);
                        foreach (string file in files)
                        {
                            Regex regex = new Regex(@"(\d+_\d+)");
                            if (regex.Match(file).Success)
                            {
                                DateTime mediafile_time = DateTime.ParseExact(regex.Match(file).Value, timeFormat, null);
                                if (mediafile_time < DateTime.Now.AddHours(retention * -1))
                                {
                                    try
                                    {
                                        writeLog(tempOutFile, "Retention clean-up of file '" + file.Replace(destination, "") + "' started...", true);
                                        request = (FtpWebRequest)WebRequest.Create(file);
                                        request.Method = WebRequestMethods.Ftp.DeleteFile;
                                        request.Credentials = cred;
                                        response = (FtpWebResponse)request.GetResponse();
                                        response.Close();
                                        removed_count++;
                                        uploadStats.removed++;
                                    }
                                    catch (Exception ex)
                                    {
                                        writeLog(tempOutFile, "Retention clean-up of file '" + file.Replace(destination, "") + "' failed due to: " + ex.Message + ".", true);
                                    }
                                }
                            }
                        }
                    }

                    // Remove directory if empty
                    file_count = file_count - removed_count;
                    if (file_count == 0)
                    {
                        try
                        {
                            writeLog(tempOutFile, "Retention clean-up of direcotry '" + directory + "' started...", true);
                            request = (FtpWebRequest)WebRequest.Create(directory);
                            request.Method = WebRequestMethods.Ftp.RemoveDirectory;
                            request.Credentials = cred;
                            response = (FtpWebResponse)request.GetResponse();
                            response.Close();
                        }
                        catch (Exception ex)
                        {
                            writeLog(tempOutFile, "Retention clean-up of direcotry '" + directory + "' failed due to: " + ex.Message + ".", true);
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                response = (FtpWebResponse)ex.Response;
                string status = response.StatusDescription.Trim();
                response.Close();
                throw new Exception(status);
            }

            // Return stats
            return uploadStats;
        }

        private bool existsFTP(string uri, NetworkCredential cred)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri.TrimEnd('/'));
            request.Credentials = cred;
            request.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            try
            {
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                response.Close();
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    // Does not exist
                    response.Close();
                    return false;
                }
                else
                {
                    response.Close();
                    throw;
                }
            }

            return true;
        }

        /*----------------------------------------------------------------------------*/

        private string bytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0 " + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + " " + suf[place];
        }

        private JToken getJson(string json, string path)
        {
            return JObject.Parse(json).SelectToken(path, false);
        }

        private string getJsonObject(string json, string path)
        {
            JToken _token = getJson(json, path);
            if (_token == null)
            {
                return String.Empty;
            }
            else
            {
                return _token.ToString();
            }
        }

        private string getJsonString(string json, string path)
        {
            return (string)getJson(json, path);
        }

        private DateTime getJsonDateTime(string json, string path)
        {
            return Convert.ToDateTime(getJson(json, path));
        }

        private int getJsonInt(string json, string path)
        {
            return (int)getJson(json, path);
        }

        private bool getJsonBool(string json, string path)
        {
            return (bool)getJson(json, path);
        }

        private bool isJson(string text)
        {
            text = text.Trim();
            if ((text.StartsWith("{") && text.EndsWith("}")) || //For object
                (text.StartsWith("[") && text.EndsWith("]"))) //For array
            {
                try
                {
                    var obj = JToken.Parse(text);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    //Exception in parsing json
                    return false;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private string BasicAuth(string username, string password)
        {
            return "Basic " + Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
        }

        private string BearerAuth(string token)
        {
            return "Bearer " + token;
        }

        private IPAddress ResolveAddress(string url)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(url, out ipAddress))
            {
                ipAddress = Dns.GetHostEntry(url).AddressList[0];
            }
            return ipAddress;
        }

        private string httpCall(string url, string method, string body, string authorization)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = 10000;
            request.ContentType = "application/json";

            if (authorization != String.Empty)
            {
                request.Headers.Add("Authorization", authorization);
            }

            if (body != String.Empty)
            {
                request.ContentLength = Encoding.UTF8.GetBytes(body).Length;

                using (StreamWriter requestWriter = new StreamWriter(request.GetRequestStream()))
                {
                    requestWriter.Write(body);
                }

            }

            try
            {
                // Handle response
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        string response_text = sr.ReadToEnd();
                        return response_text;
                    }
                }
            }
            catch (WebException ex)
            {
                // Handle response even if it throws an exception
                using (HttpWebResponse response = (HttpWebResponse)ex.Response)
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        string response_text = sr.ReadToEnd();
                        // Check if response is JSON, if yes return it else throw original exception
                        if (!isJson(response_text))
                        {
                            throw;
                        }
                        else
                        {
                            return response_text;
                        }
                    }
                }
            }
        }

        private string downloadFile(string url, string authorization)
        {
            string destination = Path.GetTempFileName();

            using (WebClient client = new WebClient())
            {
                if (authorization != String.Empty)
                {
                    client.Headers.Add("Authorization", authorization);
                }
                client.DownloadFile(host + url, destination);
            }

            return destination;
        }

        /*----------------------------------------------------------------------------*/

        public string callAPI(string api_path, string method, string body, string json_path)
        {
            // Call API
            string response = httpCall(host + api_path, method, body, BearerAuth(token));

            string output = String.Empty;

            /*
             * json_path - path in JSON structure, dollar ($) can be used to output everything
             *             otherwise path is constructed from element names connected via dot (.).
             *             Detailed documentation how to use syntax in json_path is here:
             *             https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html
             *             and online evaluator is here: https://jsonpath.com/
             */
            if (json_path != String.Empty)
            {
                output = getJsonObject(response, json_path);;
            }

            return output;
        }

        /*----------------------------------------------------------------------------*/

        public void writeLog(string filename, string new_text, bool write_to_eof)
        {
            writeLog(filename, new_text, write_to_eof, false);
        }

        public void writeLog(string filename, string new_text, bool write_to_eof, bool suppress_output)
        {
            if (configuration.writeLog)
            {
                string tempfile = Path.GetTempFileName();

                try
                {
                    if (!File.Exists(filename))
                    {
                        using (FileStream fs = File.Create(filename))
                        {
                            Byte[] info = new UTF8Encoding(true).GetBytes(String.Empty);
                            fs.Write(info, 0, info.Length);
                        }
                    }

                    bool write_success = false;
                    TimeSpan attempt_time = new TimeSpan(0, 0, 0, 0, 0);
                    TimeSpan attempt_limit = new TimeSpan(0, 0, 0, 0, 100); // days, hours, minutes, seconds, milliseconds
                    DateTime attempt_start = DateTime.Now;
                    while (!write_success)
                    {
                        try
                        {
                            using (StreamWriter writer = new StreamWriter(tempfile))
                            {
                                using (StreamReader reader = new StreamReader(filename))
                                {
                                    if (write_to_eof)
                                    {
                                        while (!reader.EndOfStream)
                                        {
                                            writer.WriteLine(reader.ReadLine());
                                        }
                                        writer.WriteLine(new_text);
                                    }
                                    else
                                    {
                                        writer.WriteLine(new_text);
                                        while (!reader.EndOfStream)
                                        {
                                            writer.WriteLine(reader.ReadLine());
                                        }
                                    }
                                }
                            }

                            write_success = true;
                        }
                        catch (Exception ex)
                        {
                            write_success = false;
                            attempt_time = DateTime.Now - attempt_start;

                            if (attempt_time > attempt_limit)
                            {
                                throw;
                            }
                        }
                    }

                    File.Copy(tempfile, filename, true);

                    if (File.Exists(tempfile))
                    {
                        File.Delete(tempfile);
                    }

                    if (!suppress_output)
                    {
                        Console.WriteLine(new_text);
                    }
                }
                catch (Exception ex)
                {
                    if (File.Exists(tempfile))
                    {
                        File.Delete(tempfile);
                    }

                    throw;
                }
            }
            else
            {
                if (!suppress_output)
                {
                    Console.WriteLine(new_text);
                }
            }
        }

        public void markLogs()
        {
            // Mark temp files with <EOF>
            if (File.Exists(tempOutFile))
            {
                if (!File.ReadAllText(tempOutFile).Trim().EndsWith("<EOF>"))
                {
                    writeLog(tempOutFile, Environment.NewLine + "<EOF>", true, true);
                }
            }

            if (File.Exists(tempErrFile))
            {
                if (!File.ReadAllText(tempErrFile).Trim().EndsWith("<EOF>"))
                {
                    writeLog(tempErrFile, Environment.NewLine + "<EOF>", true, true);
                }
            }
        }

        public void handleTempLogs()
        {
            try
            {
                // Mark temp files with <EOF>
                markLogs();

                // Stop log handling if that is configured
                if (configuration.suppressLogHandling)
                {
                    return;
                }

                // Check if other process is running
                Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
                foreach (Process process in processes)
                {
                    if (process.Id != processId)
                    {
                        if (process.MainModule.FileName == Process.GetCurrentProcess().MainModule.FileName)
                        {
                            // Other process of the same path is running, it will handle logs
                            return;
                        }
                    }
                }

                // Create flag that log handling is underway if it does not exist already
                if (!File.Exists(logHandlerFlag))
                {
                    File.Create(logHandlerFlag).Dispose();
                }
                else
                {
                    // Check if existing file is not too old
                    if (File.GetCreationTime(logHandlerFlag).AddMinutes(1) > DateTime.Now)
                    {
                        // Flag is not too old to continue
                        return;
                    }
                    else
                    {
                        // Recreate flag and continue handling
                        File.Delete(logHandlerFlag);
                        File.Create(logHandlerFlag).Dispose();
                    }
                }

                // Archive large files
                long maxFileSize = 5 * (1024*1024); // 5MB
                if (File.Exists(outFile))
                {
                    if (new FileInfo(outFile).Length > maxFileSize)
                    {
                        if (!Directory.Exists(archivePath))
                        {
                            Directory.CreateDirectory(archivePath);
                        }
                        File.Move(outFile, archivePath + "\\" + filesName + "_" + filesTimestamp + ".out");
                    }
                }
                if (File.Exists(errFile))
                {
                    if (new FileInfo(errFile).Length > maxFileSize)
                    {
                        if (!Directory.Exists(archivePath))
                        {
                            Directory.CreateDirectory(archivePath);
                        }
                        File.Move(outFile, archivePath + "\\" + filesName + "_" + filesTimestamp + ".err");
                    }
                }

                // Handle logs
                DirectoryInfo dir = new DirectoryInfo(product_location);
                FileInfo[] files = dir.GetFiles(filesName + "*.tmp", SearchOption.TopDirectoryOnly);
                Array.Sort(files, delegate (FileInfo f1, FileInfo f2) { return f1.CreationTime.CompareTo(f2.CreationTime); });
                bool output_stopped = false;
                bool error_stopped = false;
                foreach (FileInfo file in files)
                {
                    bool is_output = false;
                    bool is_error = false;
                    
                    string log_file = String.Empty;

                    if (file.Name.EndsWith(".out.tmp"))
                    {
                        log_file = outFile;
                        is_output = true;
                    }

                    if (file.Name.EndsWith(".err.tmp"))
                    {
                        log_file = errFile;
                        is_error = true;
                    }

                    if (!is_output && !is_error)
                    {
                        continue;
                    }

                    if (output_stopped && error_stopped)
                    {
                        break;
                    }

                    if ((output_stopped && is_output) || (error_stopped && is_error))
                    {
                        continue;
                    }

                    // Create log file if does not exist
                    if (!File.Exists(log_file))
                    {
                        using (FileStream fs = File.Create(log_file))
                        {
                            Byte[] info = new UTF8Encoding(true).GetBytes(String.Empty);
                            fs.Write(info, 0, info.Length);
                        }
                    }

                    // Check if file is fit for processing
                    string file_content = File.ReadAllText(file.FullName);
                    bool file_is_finished = file_content.Trim().EndsWith("<EOF>");
                    bool file_too_old = (file.LastWriteTime.AddMinutes(10) < DateTime.Now);
                    if (file_is_finished || file_too_old)
                    {
                        // Process file
                        File.AppendAllText(log_file, file_content.TrimEnd().Replace("<EOF>", String.Empty));

                        // Remove processed file
                        File.Delete(file.FullName);
                    }
                    else
                    {
                        // Stop further processing of files of same type
                        if (is_output)
                        {
                            output_stopped = true;
                        }

                        if (is_error)
                        {
                            error_stopped = true;
                        }
                    }
                }

                // Remove flag if finished
                File.Delete(logHandlerFlag);
            }
            catch (Exception ex)
            {
                // Remove flag if failed
                File.Delete(logHandlerFlag);

                throw;
            }
        }

        public static bool readConfigBool(string argName, bool if_not_set)
        {
            string value = readConfig(argName);
            bool is_set = (value == String.Empty ? false : true);
            return (is_set ? Convert.ToBoolean(value) : if_not_set);
        }

        public static DateTime readConfigTime(string argName, DateTime if_not_set)
        {
            string value = readConfig(argName);
            bool is_set = (value == String.Empty ? false : true);
            return (is_set ? DateTime.ParseExact(readConfig(argName), "yyyy.MM.dd HH:mm:ss", null) : if_not_set);
        }

        public static string readConfigString(string argName, string if_not_set)
        {
            string value = readConfig(argName);
            bool is_set = (value == String.Empty ? false : true);
            return (is_set ? value : if_not_set);
        }

        public static int readConfigInt(string argName, int if_not_set)
        {
            string value = readConfig(argName);
            bool is_set = (value == String.Empty ? false : true);
            return (is_set ? Convert.ToInt32(value) : if_not_set);
        }

        public static string readConfig(string argName)
        {
            if (File.Exists(confFile))
            {
                string[] configuration = new string[] { };
                string[] lines = File.ReadAllLines(confFile);
                string value = String.Empty;

                foreach (string line in lines)
                {
                    if (!line.StartsWith(argName + "="))
                    {
                        continue;
                    }

                    int startIndex = (argName + "=").Length;
                    int endIndex = line.Length;
                    int length = endIndex - startIndex;
                    value = line.Substring(startIndex, length).Trim();
                }

                return value;
            }
            else
            {
                return String.Empty;
            }
        }

        private static void getStackCallers()
        {
            StackTrace stackTrace = new StackTrace();
            string[] callers = new string[] { };
            for (int i = 0; ; i++)
            {
                if (stackTrace.GetFrame(i).GetILOffset() == StackFrame.OFFSET_UNKNOWN)
                {
                    break;
                }

                string class_name = stackTrace.GetFrame(i).GetMethod().ReflectedType.Name;
                string method_name = stackTrace.GetFrame(i).GetMethod().Name;
                if (method_name.StartsWith(".") || method_name == stackTrace.GetFrame(0).GetMethod().Name)
                {
                    continue;
                }

                Array.Resize(ref callers, callers.Length + 1);
                callers[callers.Length - 1] = class_name + "." + method_name;
            }

            Array.Reverse(callers);
            int order = 1;
            foreach (string caller in callers)
            {
                Console.WriteLine("Caller " + order + ": " + caller);
                order++;
            }
            Console.WriteLine("======");
        }
    }

    /* NFS connection handler - https://stackoverflow.com/a/14870774/3650856 */

    internal class ConnectToSharedFolder : IDisposable
    {
        readonly string _networkName;

        public ConnectToSharedFolder(string networkName, NetworkCredential credentials)
        {
            _networkName = networkName;

            var netResource = new NetResource
            {
                Scope = ResourceScope.GlobalNetwork,
                ResourceType = ResourceType.Disk,
                DisplayType = ResourceDisplaytype.Share,
                RemoteName = networkName
            };

            var userName = string.IsNullOrEmpty(credentials.Domain)
                ? credentials.UserName
                : string.Format(@"{0}\{1}", credentials.Domain, credentials.UserName);

            var result = WNetAddConnection2(
                netResource,
                credentials.Password,
                userName,
                0);

            if (result != 0)
            {
                string error_msg = String.Empty;

                switch (result)
                {
                    case 53:
                        error_msg = "Error connecting to remote share due to bad network path (code: '" + result + "').";
                        break;
                    case 54:
                        error_msg = "Error connecting to remote share due to busy network (code: '" + result + "').";
                        break;
                    case 65:
                        error_msg = "Error connecting to remote share due to denied network access (code: '" + result + "').";
                        break;
                    case 86:
                        error_msg = "Error connecting to remote share due to invalid password (code: '" + result + "').";
                        break;
                    case 1219:
                        error_msg = "Error connecting to remote share due to connection already existing (code: '" + result + "'). Hint: If you use same connection within windows, this error will occure. If you use IP in windows, try using host in this connection and vice versa.";
                        break;
                    case 2202:
                        error_msg = "Error connecting to remote share due to invalid username (code: '" + result + "').";
                        break;
                    default:
                        error_msg = "Error connecting to remote share with unknown code " + result + ".";
                        break;
                }
                /*
                Private Const ERROR_BAD_NETPATH = 53
                Private Const ERROR_NETWORK_ACCESS_DENIED = 65
                Private Const ERROR_INVALID_PASSWORD = 86
                Private Const ERROR_NETWORK_BUSY = 54
                Const ERROR_BAD_USERNAME = 2202
                */
                throw new Win32Exception(result, error_msg);
            }
        }

        ~ConnectToSharedFolder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            WNetCancelConnection2(_networkName, 0, true);
        }

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource netResource,
            string password, string username, int flags);

        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags,
            bool force);

        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
        {
            public ResourceScope Scope;
            public ResourceType ResourceType;
            public ResourceDisplaytype DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        public enum ResourceScope : int
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        };

        public enum ResourceType : int
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8,
        }

        public enum ResourceDisplaytype : int
        {
            Generic = 0x0,
            Domain = 0x01,
            Server = 0x02,
            Share = 0x03,
            File = 0x04,
            Group = 0x05,
            Network = 0x06,
            Root = 0x07,
            Shareadmin = 0x08,
            Directory = 0x09,
            Tree = 0x0a,
            Ndscontainer = 0x0b
        }
    }
}
