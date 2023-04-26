using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ULOControls
{
    public class Ulo
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
        private static readonly string[] SupportedVersions = new string[] { "01.0101", "08.0803", "08.0804", "08.0904", "10.1308" };

        // Private
        private static readonly string ProductLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        private string _host = String.Empty;
        private string _token = String.Empty;

        // Public
        public static readonly int processId = Process.GetCurrentProcess().Id;
        public static readonly string filesName = "ULOControls";
        public static readonly string timeFormat = "yyyyMMdd_HHmmss";
        public static readonly string filesTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        public static readonly string tempOutFile = ProductLocation + @"\" + filesName + "_" + filesTimestamp + "_" + processId + ".out.tmp";
        public static readonly string tempErrFile = ProductLocation + @"\" + filesName + "_" + filesTimestamp + "_" + processId + ".err.tmp";
        public static readonly string outFile = ProductLocation + @"\" + filesName + ".out";
        public static readonly string errFile = ProductLocation + @"\" + filesName + ".err";
        public static readonly string confFile = ProductLocation + @"\" + filesName + ".conf";
        public static readonly string archivePath = ProductLocation + @"\archive";
        public static readonly string logHandlerFlag = ProductLocation + @"\LOG_HANDLING";
        public DateTime sessionStart = DateTime.Now;
        public DateTime sessionEnd = DateTime.Now;
        public bool isSupported = false;
        public string currentVersion = String.Empty;
        public int pingTimeout = 5000;

        public Uri uri = null;

        public Configuration configuration = new Configuration();

        private void Clear()
        {
            //response = String.Empty;
            uri = null;
            _host = String.Empty;
            _token = String.Empty;
            sessionStart = DateTime.Now;
            sessionEnd = DateTime.Now;
            isSupported = false;
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
            public bool writeLog = ReadConfigBool(ConfigParams.WriteLog, false);
            public bool showArguments = ReadConfigBool(ConfigParams.ShowArguments, false);
            public bool showTrace = ReadConfigBool(ConfigParams.ShowTrace, false);
            public bool showSkipped = ReadConfigBool(ConfigParams.ShowSkipped, false);
            public bool showPingResults = ReadConfigBool(ConfigParams.ShowPingResults, false);
            public bool suppressLogHandling = ReadConfigBool(ConfigParams.SuppressLogHandling, false);
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
            public const string Nfs = "nfs";
            public const string Ftp = "ftp";
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

        public void ReloadConfiguration()
        {
            configuration = new Configuration();
        }

        public bool Ping(string host, bool output)
        {
            if (!host.Contains("://"))
            {
                host = "https://" + host;
            }
            host = new Uri(host).Host;

            Ping ping = new Ping();

            string data = new String('.', 32);
            int packetSize = Encoding.UTF8.GetByteCount(data);
            byte[] buffer = Encoding.UTF8.GetBytes(data);

            PingOptions options = new PingOptions(64, true);
            PingReply reply = ping.Send(host, pingTimeout, buffer, options);

            bool state = false;

            if (reply != null && reply.Status == IPStatus.Success)
            {
                if (output)
                {
                    WriteLog(tempOutFile, "Address:             " + reply.Address, true);
                    WriteLog(tempOutFile, "RoundTrip time [ms]: " + reply.RoundtripTime, true);
                    WriteLog(tempOutFile, "Time to live [hops]: " + reply.Options.Ttl, true);
                    WriteLog(tempOutFile, "Don't fragment:      " + reply.Options.DontFragment, true);
                    WriteLog(tempOutFile, "Buffer size [bytes]: " + reply.Buffer.Length, true);
                }

                state = true;
            }
            else
            {
                if (output)
                {
                    if (reply != null) WriteLog(tempOutFile, "Ping failed with status: " + reply.Status, true);
                }

                state = false;
            }

            return state;
        }

        public void TestAvailability(string host)
        {
            // Test for device availability
            if (Ping(host, true))
            {
                WriteLog(tempOutFile, "", true);
                WriteLog(tempOutFile, "Device is available.", true);
            }
            else
            {
                WriteLog(tempOutFile, "", true);
                WriteLog(tempOutFile, "Device is NOT available.", true);
            }
        }

        public void CheckAvailability(string ifTrue, string ifFalse, string operation, string host1, string host2, string host3, string host4, string host5)
        {
            try
            {
                // Check for device availability and set proper mode
                WriteLog(tempOutFile, "", true);
                WriteLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);

                string[] hosts = new string[] { };
                bool result = false;
                bool resultStep = false;

                switch (operation)
                {
                    case Operation.And:
                        resultStep = true;
                        break;
                    case Operation.Or:
                        resultStep = false;
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
                    result = Ping(host, false);

                    if (configuration.showPingResults)
                    {
                        WriteLog(tempOutFile, "Host:                    " + host, true);
                        WriteLog(tempOutFile, "Operation:               " + operation, true);
                        WriteLog(tempOutFile, "Result:                  " + result, true);
                        WriteLog(tempOutFile, "Result step before eval: " + resultStep, true);
                    }

                    switch (operation)
                    {
                        case Operation.And:
                            if (result && resultStep) { resultStep = true; } else { resultStep = false; }
                            break;
                        case Operation.Or:
                            if (result || resultStep) { resultStep = true; } else { resultStep = false; }
                            break;
                        default:
                            throw new Exception("Operation '" + operation + "' is not supported.");
                    }

                    if (configuration.showPingResults)
                    {
                        WriteLog(tempOutFile, "Result step after eval:  " + resultStep, true);
                        WriteLog(tempOutFile, "===============================================", true);
                    }
                }

                if (resultStep)
                {
                    switch (operation)
                    {
                        case Operation.And:
                            WriteLog(tempOutFile, "All devices are available.", true);
                            break;
                        case Operation.Or:
                            WriteLog(tempOutFile, "At least one device is available.", true);
                            break;
                        default:
                            throw new Exception("Operation '" + operation + "' is not supported.");
                    }

                    // Set mode based on provided value
                    WriteLog(tempOutFile, "Setting camera to '" + ifTrue + "' mode.", true);
                    SetMode(ifTrue);
                }
                else
                {
                    switch (operation)
                    {
                        case Operation.And:
                            WriteLog(tempOutFile, "At least one device is not available.", true);
                            break;
                        case Operation.Or:
                            WriteLog(tempOutFile, "All devices are not available.", true);
                            break;
                        default:
                            throw new Exception("Operation '" + operation + "' is not supported.");
                    }

                    // Set mode based on provided value
                    WriteLog(tempOutFile, "Setting camera to '" + ifFalse + "' mode.", true);
                    SetMode(ifFalse);
                }
            }
            catch (Exception ex) {
                try
                {
                    SetMode(ifFalse);
                }
                catch (Exception exi)
                {
                    // ignored
                }

                throw;
            }
        }

        public string Login(string uloHost, string username, string password)
        {
            // Workaround for invalid certificate
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Login
            if (!uloHost.Contains("://"))
            {
                uloHost = "https://" + uloHost;
            }
            uri = new Uri(uloHost);
            _host = "https://" + uri.Host;
            string response = HttpCall(_host + "/api/v1/login", "POST", "{ \"iOSAgent\": false }", BasicAuth(username, password));
            sessionStart = DateTime.Now;
            _token = GetJsonString(response, "token");
            sessionEnd = sessionStart.AddSeconds(GetJsonInt(response, "expiresIn"));

            // Check version support
            isSupported = CheckVersion();

            return _token;
        }

        public void Logout()
        {
            // Logout
            if (_token != String.Empty)
            {
                CallApi("/api/v1/logout", "POST", "{}", String.Empty);
            }

            Clear();
        }

        public DateTime GetUloTime()
        {
            // Get time
            DateTime uloTime = Convert.ToDateTime(CallApi("/api/v1/time", "GET", String.Empty, "time"));
            if (uloTime == new DateTime(1970, 1, 1, 0, 0, 0))
            {
                uloTime = DateTime.Now;
            }

            return uloTime;
        }

        public bool CheckVersion()
        {
            // Check version
            bool supported = false;
            currentVersion = CallApi("/api/v1/config", "GET", String.Empty, "firmware.currentversion");
            foreach (string supportedVersion in SupportedVersions)
            {
                if (supportedVersion == currentVersion)
                {
                    supported = true;
                }
            }
            if (!supported)
            {
                WriteLog(tempOutFile, "WARNING: Your current ULO version (" + currentVersion + ") is not tested, yet. Results may vary.", true);
            }

            return supported;
        }

        public string GetMode()
        {
            // Get mode
            return CallApi("/api/v1/mode", "GET", String.Empty, "mode");
        }

        public void SetMode(string mode)
        {
            // Set mode
            if (CallApi("/api/v1/mode", "PUT", "{ \"mode\": \"" + mode + "\" }", "mode").ToLower() != mode)
            {
                throw new Exception("Mode change failed.");
            }
            else
            {
                WriteLog(tempOutFile, "Success.", true);
            }
        }

        public bool IsPowered()
        {
            // Get info if ULO is powered by electricity from plug
            return Convert.ToBoolean(CallApi("/api/v1/state", "GET", String.Empty, "plugged"));
        }

        public int GetBattery()
        {
            // Get battery capacity
            return Convert.ToInt32(CallApi("/api/v1/state", "GET", String.Empty, "batteryLevel"));
        }

        public bool IsCard()
        {
            // Get info if SD card is inserted into ULO
            return Convert.ToBoolean(CallApi("/api/v1/files/stats", "GET", String.Empty, "sdcard.inserted"));
        }

        public int GetCardSpace()
        {
            // Get SD card free capacity
            return Convert.ToInt32(CallApi("/api/v1/files/stats", "GET", String.Empty, "sdcard.freeMB"));
        }

        public int GetDiskSpace()
        {
            // Get internal memory free capacity
            return Convert.ToInt32(CallApi("/api/v1/files/stats", "GET", String.Empty, "internal.freeMB"));
        }

        public void MoveToCard()
        {
            // Move files from internal memory to SD card
            string modeBackup = GetMode();
            SetMode(CameraMode.Standard);
            string response = CallApi("/api/v1/files/backup?filename=all", "PUT", "{\"running\": true}", "$");
            SetMode(modeBackup);
            
            string error = GetJsonString(response, "error");
            if (!String.IsNullOrEmpty(error))
            {
                throw new Exception(error);
            }
            else
            {
                string status = GetJsonString(response, "status");
                WriteLog(tempOutFile, status, true);
            }
        }

        public void CleanDiskSpace(string period)
        {
            // Clean files on internal memory
            string modeBackup = GetMode();
            SetMode(CameraMode.Standard);
            string response = CallApi("/api/v1/files/delete?removeType=" + period, "DELETE", String.Empty, "$");
            SetMode(modeBackup);

            string error = GetJsonString(response, "error");
            if (error != String.Empty)
            {
                throw new Exception(error);
            }
            else
            {
                string status = GetJsonString(response, "status");
                WriteLog(tempOutFile, status, true);
            }
        }

        public void DownloadLog(string type, string destination, int retention, string username, string password)
        {
            // Download ULO log into specified location
            string logLocation = CallApi("/api/v1/system/log", "POST", "/system/log", "fileName").Replace("\n", Environment.NewLine);
            UploadStatistics logStats = new UploadStatistics();
            logStats = UploadHandler(type, "/logs/" + logLocation, destination, false, username, password);
            logStats = RetentionHandler(type, destination, retention, MediaTypeExt.Log, username, password);
        }

        public void DownloadCurrent(string type, string destination, string username, string password)
        {
            // Download current snapshot
            UploadStatistics fileStats = UploadHandler(type, "/" + CallApi("/api/v1/backgroundImage", "POST", "{}", "filename"), destination, true, username, password);
        }

        public void DownloadMedia(string mediatype, string type, string destination, int age, int retention, string username, string password)
        {
            // Download all media requested and maintain them
            WriteLog(tempOutFile, "", true);
            WriteLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);

            Regex regex = null;
            Match match = null;
            string[] index = new string[] { };
            string[] mediafiles = new string[] { };
            string mediatypeExtension = null;
            string response = String.Empty;

            // Set values based on mediatype
            switch (mediatype)
            {
                case MediaType.Video:
                    mediatypeExtension = MediaTypeExt.Video;
                    break;
                case MediaType.Snapshot:
                    mediatypeExtension = MediaTypeExt.Snapshot;
                    break;
                default:
                    throw new Exception("Media type '" + mediatype + "' is not supported.");
            }

            // Get list of folders
            index = GetJsonStringArray(CallApi("/api/v1/files/media", "GET", "", "$"), "$.files[*].files[*]");
            regex = new Regex(@"(media\/\d+/video_\d+_\d+." + mediatypeExtension + ")");
            foreach (string part in index)
            {
                match = regex.Match(part);
                if (match.Success)
                {
                    Array.Resize(ref mediafiles, mediafiles.Length + 1);
                    mediafiles[mediafiles.Length - 1] = "/" + match.Value;
                }
            }

            // Download media files
            regex = new Regex(@"(\d+_\d+)");
            DateTime uloTime = GetUloTime();
            DateTime ageTime = uloTime.AddHours(age * -1);
            DateTime freshFileTime = uloTime.AddMinutes(-1);
            UploadStatistics totalStats = new UploadStatistics();
            bool stopProcessing = false;
            foreach (string mediafile in mediafiles)
            {
                UploadStatistics fileStats = new UploadStatistics();

                try
                {
                    // Check age
                    string mediafilename = Path.GetFileName(mediafile.Replace("/", "\\"));
                    DateTime mediafileTime = DateTime.ParseExact(regex.Match(mediafilename).Value, timeFormat, null);

                    if (age != 0 && mediafileTime < ageTime)
                    {
                        fileStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            WriteLog(tempOutFile, "Media file '" + mediafile + "' is too old based on age settings...", true);
                            WriteLog(tempOutFile, "Skipped.", true);
                        }
                    }
                    else
                    {
                        // Check if last media file is old enough to be downloaded
                        if (mediafiles[mediafiles.Length - 1] == mediafile && mediafileTime > freshFileTime)
                        {
                            fileStats.skipped = 1;
                            WriteLog(tempOutFile, "Media file '" + mediafile + "' might be still used by ULO, it will be downloaded later...", true);
                            WriteLog(tempOutFile, "Skipped.", true);
                        }
                        else
                        {
                            try
                            {
                                fileStats = UploadHandler(type, mediafile, destination, false, username, password);
                            }
                            catch (Exception ex)
                            {
                                stopProcessing = false;
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    fileStats.failed = 1;
                    WriteLog(tempOutFile, "Failed. (Error: " + ex.Message.Trim() + ")", true);

                    if (stopProcessing)
                    {
                        throw;
                    }
                    else
                    {
                        string errorOutput = String.Empty;
                        errorOutput += DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]") + Environment.NewLine;
                        errorOutput += "HelpLink   = " + ex.HelpLink + Environment.NewLine;
                        errorOutput += "Message    = " + ex.Message + Environment.NewLine;
                        errorOutput += "Source     = " + ex.Source + Environment.NewLine;
                        errorOutput += "StackTrace = " + ex.StackTrace + Environment.NewLine;
                        errorOutput += "TargetSite = " + ex.TargetSite + Environment.NewLine;

                        if (configuration.showTrace)
                        {
                            Console.WriteLine(String.Empty);
                            Console.WriteLine(errorOutput);
                        }

                        WriteLog(tempErrFile, errorOutput, false, true);
                    }
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
                WriteLog(tempOutFile, "Retention clean-up...", true);

                // Perform clean-up based on retention
                UploadStatistics retentionStats = new UploadStatistics();

                try
                {
                    retentionStats = RetentionHandler(type, destination, retention, mediatypeExtension, username, password);
                }
                catch (Exception ex)
                {
                    stopProcessing = false;
                    throw;
                }

                totalStats.removed = totalStats.removed + retentionStats.removed;
            }

            // Output summary
            WriteLog(tempOutFile, "=====================================", true);
            WriteLog(tempOutFile, "                DONE                 ", true);
            WriteLog(tempOutFile, "=====================================", true);
            WriteLog(tempOutFile, DateTime.Now.ToString("[yyyy.MM.dd - HH:mm:ss]"), true);
            WriteLog(tempOutFile, "Stats:", true);
            WriteLog(tempOutFile, "     Media type:        " + mediatype, true);
            WriteLog(tempOutFile, "     Files uploaded:    " + totalStats.succeeded, true);
            WriteLog(tempOutFile, "     Files failed:      " + totalStats.failed, true);
            WriteLog(tempOutFile, "     Files skipped:     " + totalStats.skipped, true);
            WriteLog(tempOutFile, "     Files overwritten: " + totalStats.overwritten, true);
            WriteLog(tempOutFile, "     Files removed:     " + totalStats.removed, true);
            WriteLog(tempOutFile, "     Files size:        " + BytesToString(totalStats.fileSize), true);
            WriteLog(tempOutFile, "     Download time:     " + Math.Round(totalStats.downloadTime, 2) + " sec", true);
            WriteLog(tempOutFile, "     Upload time:       " + Math.Round(totalStats.uploadTime, 2) + " sec", true);
            WriteLog(tempOutFile, "     Down&Up time:      " + Math.Round(totalStats.downloadTime + totalStats.uploadTime, 2) + " sec", true);
            WriteLog(tempOutFile, "     Avg. down. speed:  " + BytesToString((long)Math.Round(totalStats.fileSize / (totalStats.downloadTime + 1), 2)) + "/s", true);
            WriteLog(tempOutFile, "     Avg. up. speed:    " + BytesToString((long)Math.Round(totalStats.fileSize / (totalStats.uploadTime + 1), 2)) + "/s", true);
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics FsHandler(string type, string fsAction, string source, string destination, int retention, bool overwrite, string mediatypeExtension, string username, string password)
        {
            UploadStatistics handlingStats = new UploadStatistics();

            switch (type)
            {
                case DestinationType.Local:
                    switch (fsAction)
                    {
                        case FsAction.Upload:
                            handlingStats = UploadLocal(source, destination, overwrite);
                            break;
                        case FsAction.Retention:
                            handlingStats = RetentionLocal(destination, retention, mediatypeExtension);
                            break;
                    }
                    break;
                case DestinationType.Nfs:
                    // Open connection to NFS
                    NetworkCredential credentials = new NetworkCredential(username, password);
                    using (ConnectToSharedFolder nfsConnection = new ConnectToSharedFolder(destination, credentials))
                    {
                        // NFS is used like Local, just needs connection to NFS upfront
                        switch (fsAction)
                        {
                            case FsAction.Upload:
                                handlingStats = UploadLocal(source, destination, overwrite);
                                break;
                            case FsAction.Retention:
                                handlingStats = RetentionLocal(destination, retention, mediatypeExtension);
                                break;
                        }
                    }
                    break;
                case DestinationType.Ftp:
                    switch (fsAction)
                    {
                        case FsAction.Upload:
                            handlingStats = UploadFtp(source, destination, overwrite, username, password);
                            break;
                        case FsAction.Retention:
                            handlingStats = RetentionFtp(destination, retention, mediatypeExtension, username, password);
                            break;
                    }
                    break;
                default:
                    throw new Exception("Destination type '" + type + "' is not supported.");
            }

            return handlingStats;
        }

        private UploadStatistics UploadHandler(string type, string source, string destination, bool overwrite, string username, string password)
        {
            return FsHandler(type, FsAction.Upload, source, destination, 0, overwrite, String.Empty, username, password);
        }

        private UploadStatistics RetentionHandler(string type, string destination, int retention, string mediatypeExtension, string username, string password)
        {
            return FsHandler(type, FsAction.Retention, String.Empty, destination, retention, false, mediatypeExtension, username, password);
        }

        private string MediaNameAdjust(string mediafilename)
        {
            // Name conversion is needed so FTP retention has same file format with date and time available
            string mediafilenameAdjusted = mediafilename;

            Regex regexSortableIso = new Regex(@"(\d+-\d+-\d+T\d+-\d+-\d+)"); // 2019-09-07T12-20-33
            Regex regexSortable = new Regex(@"(\d+_\d+)"); // 20190907_122033
            Regex regexLogin = new Regex(@"(loginPicture\.jpg)"); // loginPicture.jpg

            if (regexSortableIso.Match(mediafilename).Success)
            {
                DateTime mediafileTime = DateTime.ParseExact(regexSortableIso.Match(mediafilename).Value, "yyyy-MM-ddTHH-mm-ss", null);
                mediafilenameAdjusted = regexSortableIso.Replace(mediafilename, mediafileTime.ToString(timeFormat));
            }
            else if (regexSortable.Match(mediafilename).Success)
            {
                DateTime mediafileTime = DateTime.ParseExact(regexSortable.Match(mediafilename).Value, "yyyyMMdd_HHmmss", null);
                mediafilenameAdjusted = regexSortable.Replace(mediafilename, mediafileTime.ToString(timeFormat));
            }
            else if (regexLogin.Match(mediafilename).Success)
            {
                mediafilenameAdjusted = regexLogin.Replace(mediafilename, "snapshot.jpg");
            }
            
            return mediafilenameAdjusted;
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics UploadLocal(string mediafile, string destination, bool overwrite)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            // Create names and paths
            bool isLocal = File.Exists(mediafile);
            string mediafilename = MediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
            string mediafileToPath = MediaNameAdjust(mediafile.Replace("/", "\\"));
            string pathto = mediafileToPath.Replace(mediafilename, "").Replace("\\media", "").Replace("\\logs", "");
            string fullPath = destination.TrimEnd('\\') + pathto + mediafilename;
            string source = String.Empty;

            if (isLocal)
            {
                mediafilename = MediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
                mediafileToPath = String.Empty;
                pathto = "\\";
                fullPath = destination.TrimEnd('\\') + pathto + mediafilename;
                source = mediafile.Replace("/", "\\");
            }

            try
            {
                // Check if media file already exists at destination
                if (File.Exists(fullPath))
                {
                    if (!overwrite)
                    {
                        uploadStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            WriteLog(tempOutFile, "Media file '" + mediafile + "' already downloaded...", true);
                            WriteLog(tempOutFile, "Skipped.", true);
                        }
                        return uploadStats;
                    }
                    else
                    {
                        uploadStats.overwritten = 1;
                    }
                }

                WriteLog(tempOutFile, "Media file '" + mediafile + "' downloading...", true);

                // Create destination folder
                Directory.CreateDirectory(destination.TrimEnd('\\') + pathto);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                
                // Download media file
                DateTime downloadstart = DateTime.Now;
                if (!isLocal)
                {
                    source = DownloadFile(mediafile, BearerAuth(_token));
                }
                DateTime downloadend = DateTime.Now;
                double downloadtime = (downloadend - downloadstart).TotalSeconds;
                long filesize = new FileInfo(source).Length;
                
                // Upload mediafile
                DateTime uploadstart = DateTime.Now;
                File.Move(source, fullPath);
                DateTime uploadend = DateTime.Now;
                double uploadtime = (uploadend - uploadstart).TotalSeconds;

                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                // Collect stats
                uploadStats.fileSize = filesize;
                uploadStats.downloadTime = downloadtime;
                uploadStats.uploadTime = uploadtime;
                uploadStats.succeeded = 1;

                WriteLog(tempOutFile, "Succeeded. (Size: " + BytesToString(filesize) + " | Time: " + Math.Round(downloadtime + uploadtime, 2) + " sec | Download speed: " + BytesToString((long)Math.Round(filesize / (downloadtime + 1), 2)) + "/s | Upload speed: " + BytesToString((long)Math.Round(filesize / (uploadtime + 1), 2)) + "/s)", true);
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

        private UploadStatistics RetentionLocal(string destination, int retention, string mediatypeExtension)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            string[] directories = Directory.GetDirectories(destination);
            Array.Sort(directories);
            foreach (string directory in directories)
            {
                WriteLog(tempOutFile, "Retention clean-up of files in directory '" + directory + "' started...", true);

                string[] files = Directory.GetFiles(directory);
                Array.Sort(files);
                foreach (string file in files)
                {
                    if (Path.GetExtension(file).ToLower() != "." + mediatypeExtension)
                    {
                        continue;
                    }

                    FileInfo fi = new FileInfo(file);
                    if (fi.CreationTime < DateTime.Now.AddHours(retention * -1))
                    {
                        try
                        {
                            WriteLog(tempOutFile, "Retention clean-up of file '" + directory.Replace(destination, "") + "\\" + fi.Name + "' started...", true);
                            fi.Delete();
                            uploadStats.removed++;
                        }
                        catch (Exception ex)
                        {
                            WriteLog(tempOutFile, "Retention clean-up of file '" + directory.Replace(destination, "") + "\\" + fi.Name + "' failed due to: " + ex.Message + ".", true);
                        }
                    }
                }

                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    try
                    {
                        WriteLog(tempOutFile, "Retention clean-up of directory '" + directory + "' started...", true);
                        Directory.Delete(directory, false);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(tempOutFile, "Retention clean-up of directory '" + directory + "' failed due to: " + ex.Message + ".", true);
                    }
                }
            }
            
            // Return stats
            return uploadStats;
        }

        /*----------------------------------------------------------------------------*/

        private UploadStatistics UploadFtp(string mediafile, string destination, bool overwrite, string username, string password)
        {
            UploadStatistics uploadStats = new UploadStatistics();

            // Create names and paths
            bool isLocal = File.Exists(mediafile);
            string mediafilename = MediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
            string mediafileToPath = MediaNameAdjust(mediafile);
            string pathto = mediafileToPath.Replace(mediafilename, "").Replace("/media", "").Replace("/logs", "");
            string fullPath = destination.TrimEnd('/') + pathto + mediafilename;
            string source = String.Empty;
            FtpWebRequest request = null;
            FtpWebResponse response = null;
            NetworkCredential cred = new NetworkCredential(username, password);

            if (isLocal)
            {
                mediafilename = MediaNameAdjust(Path.GetFileName(mediafile.Replace("/", "\\")));
                mediafileToPath = String.Empty;
                pathto = "\\";
                fullPath = destination.TrimEnd('\\') + pathto + mediafilename;
                source = mediafile.Replace("/", "\\");
            }

            // Check if media file already exists at destination
            try
            {
                if (ExistsFtp(fullPath, cred))
                {
                    if (!overwrite)
                    {
                        uploadStats.skipped = 1;
                        if (configuration.showSkipped)
                        {
                            WriteLog(tempOutFile, "Media file '" + mediafile + "' already downloaded...", true);
                            WriteLog(tempOutFile, "Skipped.", true);
                        }
                        return uploadStats;
                    }
                    else
                    {
                        uploadStats.overwritten = 1;
                    }
                }

                WriteLog(tempOutFile, "Media file '" + mediafile + "' downloading...", true);

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
                if (!isLocal)
                {
                    source = DownloadFile(mediafile, BearerAuth(_token));
                }
                DateTime downloadend = DateTime.Now;
                double downloadtime = (downloadend - downloadstart).TotalSeconds;
                long filesize = new FileInfo(source).Length;

                // Upload media file
                WebClient client = new WebClient();
                client.Credentials = cred;

                DateTime uploadstart = DateTime.Now;
                client.UploadFile(fullPath, WebRequestMethods.Ftp.UploadFile, source);
                DateTime uploadend = DateTime.Now;
                double uploadtime = (uploadend - uploadstart).TotalSeconds;

                client.Dispose();

                // Make sure no temporary files are kept
                if (File.Exists(source))
                {
                    File.Delete(source);
                }

                // Collect stats
                uploadStats.fileSize = filesize;
                uploadStats.downloadTime = downloadtime;
                uploadStats.uploadTime = uploadtime;
                uploadStats.succeeded = 1;

                WriteLog(tempOutFile, "Succeeded. (Size: " + BytesToString(filesize) + " | Time: " + Math.Round(downloadtime + uploadtime, 2) + " sec | Download speed: " + BytesToString((long)Math.Round(filesize / (downloadtime + 1), 2)) + "/s | Upload speed: " + BytesToString((long)Math.Round(filesize / (uploadtime + 1), 2)) + "/s)", true);
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

        private UploadStatistics RetentionFtp(string destination, int retention, string mediatypeExtension, string username, string password)
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
                    using (streamReader = new StreamReader(response.GetResponseStream() ?? throw new InvalidOperationException()))
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
                    int fileCount = 0;
                    using (response = (FtpWebResponse)request.GetResponse())
                    {
                        using (streamReader = new StreamReader(response.GetResponseStream()))
                        {
                            string line = streamReader.ReadLine();
                            while (!string.IsNullOrEmpty(line))
                            {
                                string fileName = line.Split(new[] { ' ', '\t' })[line.Split(new[] { ' ', '\t' }).Length - 1];

                                if (fileName == "." || fileName == "..")
                                {
                                    line = streamReader.ReadLine();
                                    continue;
                                }

                                fileCount++;

                                if (Path.GetExtension(fileName).ToLower() != "." + mediatypeExtension)
                                {
                                    line = streamReader.ReadLine();
                                    continue;
                                }

                                Array.Resize(ref files, files.Length + 1);
                                files[files.Length - 1] = directory.TrimEnd('/') + "/" + fileName;
                                line = streamReader.ReadLine();
                            }
                        }
                    }

                    // Remove files if too old
                    int removedCount = 0;
                    if (files.Length != 0)
                    {
                        Array.Sort(files);
                        foreach (string file in files)
                        {
                            Regex regex = new Regex(@"(\d+_\d+)");
                            if (regex.Match(file).Success)
                            {
                                DateTime mediafileTime = DateTime.ParseExact(regex.Match(file).Value, timeFormat, null);
                                if (mediafileTime < DateTime.Now.AddHours(retention * -1))
                                {
                                    try
                                    {
                                        WriteLog(tempOutFile, "Retention clean-up of file '" + file.Replace(destination, "") + "' started...", true);
                                        request = (FtpWebRequest)WebRequest.Create(file);
                                        request.Method = WebRequestMethods.Ftp.DeleteFile;
                                        request.Credentials = cred;
                                        response = (FtpWebResponse)request.GetResponse();
                                        response.Close();
                                        removedCount++;
                                        uploadStats.removed++;
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteLog(tempOutFile, "Retention clean-up of file '" + file.Replace(destination, "") + "' failed due to: " + ex.Message + ".", true);
                                    }
                                }
                            }
                        }
                    }

                    // Remove directory if empty
                    fileCount = fileCount - removedCount;
                    if (fileCount == 0)
                    {
                        try
                        {
                            WriteLog(tempOutFile, "Retention clean-up of directory '" + directory + "' started...", true);
                            request = (FtpWebRequest)WebRequest.Create(directory);
                            request.Method = WebRequestMethods.Ftp.RemoveDirectory;
                            request.Credentials = cred;
                            response = (FtpWebResponse)request.GetResponse();
                            response.Close();
                        }
                        catch (Exception ex)
                        {
                            WriteLog(tempOutFile, "Retention clean-up of directory '" + directory + "' failed due to: " + ex.Message + ".", true);
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

        private bool ExistsFtp(string ftpUri, NetworkCredential cred)
        {
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ftpUri.TrimEnd('/'));
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

        private string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0 " + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString(CultureInfo.InvariantCulture) + " " + suf[place];
        }

        private IEnumerable<JToken> GetJson(string json, string path)
        {
            return JObject.Parse(json).SelectTokens(path, false);
        }

        private string GetJsonObject(string json, string path)
        {
            IEnumerable<JToken> tokens = GetJson(json, path);

            if (tokens == null)
            {
                return String.Empty;
            }
            else
            {
                using (IEnumerator<JToken> iter = tokens.GetEnumerator())
                {
                    iter.MoveNext();
                    return iter.Current?.ToString();
                }
            }
        }

        private string[] GetJsonStringArray(string json, string path)
        {
            string[] cobject = new string[] { };
            foreach (JToken jobject in GetJson(json, path))
            {
                Array.Resize(ref cobject, cobject.Length + 1);
                cobject[cobject.Length - 1] = Convert.ToString(jobject);
            }
            return cobject;
        }

        private int[] GetJsonIntArray(string json, string path)
        {
            int[] cobject = new int[] { };
            foreach (JToken jobject in GetJson(json, path))
            {
                Array.Resize(ref cobject, cobject.Length + 1);
                cobject[cobject.Length - 1] = Convert.ToInt32(jobject);
            }
            return cobject;
        }

        private string GetJsonString(string json, string path)
        {
            using (IEnumerator<JToken> iter = GetJson(json, path).GetEnumerator())
            {
                iter.MoveNext();
                return Convert.ToString(iter.Current);
            }
        }

        private DateTime GetJsonDateTime(string json, string path)
        {
            using (IEnumerator<JToken> iter = GetJson(json, path).GetEnumerator())
            {
                iter.MoveNext();
                return Convert.ToDateTime(iter.Current);
            }
        }

        private int GetJsonInt(string json, string path)
        {
            using (IEnumerator<JToken> iter = GetJson(json, path).GetEnumerator())
            {
                iter.MoveNext();
                return Convert.ToInt32(iter.Current);
            }
        }

        private bool GetJsonBool(string json, string path)
        {
            using (IEnumerator<JToken> iter = GetJson(json, path).GetEnumerator())
            {
                iter.MoveNext();
                return Convert.ToBoolean(iter.Current);
            }
        }

        private bool IsJson(string text)
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

        private string HttpCall(string url, string method, string body, string authorization)
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
                    using (StreamReader sr = new StreamReader(response.GetResponseStream() ?? throw new InvalidOperationException()))
                    {
                        string responseText = sr.ReadToEnd();
                        return responseText;
                    }
                }
            }
            catch (WebException ex)
            {
                // Handle response even if it throws an exception
                using (HttpWebResponse response = (HttpWebResponse)ex.Response)
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream() ?? throw new InvalidOperationException()))
                    {
                        string responseText = sr.ReadToEnd();
                        // Check if response is JSON, if yes return it else throw original exception
                        if (!IsJson(responseText))
                        {
                            throw;
                        }
                        else
                        {
                            return responseText;
                        }
                    }
                }
            }
        }

        private class WebClientWithTimeout : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest wr = base.GetWebRequest(address);
                wr.Timeout = 5000; // timeout in milliseconds (ms)
                return wr;
            }
        }

        private string DownloadFile(string url, string authorization)
        {
            string destination = Path.GetTempFileName();

            using (WebClient client = new WebClientWithTimeout())
            {
                if (authorization != String.Empty)
                {
                    client.Headers.Add("Authorization", authorization);
                }
                Stream stream = client.OpenRead(_host + url);
                Int64 webFileSize = Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                client.DownloadFile(_host + url, destination);
                Int64 localFileSize = new FileInfo(destination).Length;
                if (stream != null) stream.Close();

                // Check if file was downloaded correctly
                if (webFileSize != localFileSize)
                {
                    try
                    {
                        File.Delete(destination);
                    }
                    catch (Exception ex)
                    {
                        // ignored
                    }

                    throw new Exception("Downloaded file size does not match original file size. (Original: " + webFileSize + "; Downloaded: " + localFileSize + ")");
                }
            }

            return destination;
        }

        /*----------------------------------------------------------------------------*/

        public string CallApi(string apiPath, string method, string body, string jsonPath)
        {
            // Call API
            string response = HttpCall(_host + apiPath, method, body, BearerAuth(_token));

            string output = String.Empty;

            /*
             * json_path - path in JSON structure, dollar ($) can be used to output everything
             *             otherwise path is constructed from element names connected via dot (.).
             *             Detailed documentation how to use syntax in json_path is here:
             *             https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html
             *             and online evaluator is here: https://jsonpath.com/
             */
            if (jsonPath != String.Empty)
            {
                output = GetJsonObject(response, jsonPath);
            }

            return output;
        }

        /*----------------------------------------------------------------------------*/

        public void WriteLog(string filename, string newText, bool writeToEof)
        {
            WriteLog(filename, newText, writeToEof, false);
        }

        public void WriteLog(string filename, string newText, bool writeToEof, bool suppressOutput)
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

                    bool writeSuccess = false;
                    TimeSpan attemptTime = new TimeSpan(0, 0, 0, 0, 0);
                    TimeSpan attemptLimit = new TimeSpan(0, 0, 0, 0, 100); // days, hours, minutes, seconds, milliseconds
                    DateTime attemptStart = DateTime.Now;
                    while (!writeSuccess)
                    {
                        try
                        {
                            using (StreamWriter writer = new StreamWriter(tempfile))
                            {
                                using (StreamReader reader = new StreamReader(filename))
                                {
                                    if (writeToEof)
                                    {
                                        while (!reader.EndOfStream)
                                        {
                                            writer.WriteLine(reader.ReadLine());
                                        }
                                        writer.WriteLine(newText);
                                    }
                                    else
                                    {
                                        writer.WriteLine(newText);
                                        while (!reader.EndOfStream)
                                        {
                                            writer.WriteLine(reader.ReadLine());
                                        }
                                    }
                                }
                            }

                            writeSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            writeSuccess = false;
                            attemptTime = DateTime.Now - attemptStart;

                            if (attemptTime > attemptLimit)
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

                    if (!suppressOutput)
                    {
                        Console.WriteLine(newText);
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
                if (!suppressOutput)
                {
                    Console.WriteLine(newText);
                }
            }
        }

        public void MarkLogs()
        {
            // Mark temp files with <EOF>
            if (File.Exists(tempOutFile))
            {
                if (!File.ReadAllText(tempOutFile).Trim().EndsWith("<EOF>"))
                {
                    WriteLog(tempOutFile, Environment.NewLine + "<EOF>", true, true);
                }
            }

            if (File.Exists(tempErrFile))
            {
                if (!File.ReadAllText(tempErrFile).Trim().EndsWith("<EOF>"))
                {
                    WriteLog(tempErrFile, Environment.NewLine + "<EOF>", true, true);
                }
            }
        }

        public void HandleTempLogs()
        {
            try
            {
                // Mark temp files with <EOF>
                MarkLogs();

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
                        var processModule = Process.GetCurrentProcess().MainModule;
                        if (processModule != null && process.MainModule != null && process.MainModule.FileName == processModule.FileName)
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
                        File.Move(errFile, archivePath + "\\" + filesName + "_" + filesTimestamp + ".err");
                    }
                }

                // Handle logs
                DirectoryInfo dir = new DirectoryInfo(ProductLocation);
                FileInfo[] files = dir.GetFiles(filesName + "*.tmp", SearchOption.TopDirectoryOnly);
                Array.Sort(files, delegate (FileInfo f1, FileInfo f2) { return f1.CreationTime.CompareTo(f2.CreationTime); });
                bool outputStopped = false;
                bool errorStopped = false;
                foreach (FileInfo file in files)
                {
                    bool isOutput = false;
                    bool isError = false;
                    
                    string logFile = String.Empty;

                    if (file.Name.EndsWith(".out.tmp"))
                    {
                        logFile = outFile;
                        isOutput = true;
                    }

                    if (file.Name.EndsWith(".err.tmp"))
                    {
                        logFile = errFile;
                        isError = true;
                    }

                    if (!isOutput && !isError)
                    {
                        continue;
                    }

                    if (outputStopped && errorStopped)
                    {
                        break;
                    }

                    if ((outputStopped && isOutput) || (errorStopped && isError))
                    {
                        continue;
                    }

                    // Create log file if does not exist
                    if (!File.Exists(logFile))
                    {
                        using (FileStream fs = File.Create(logFile))
                        {
                            Byte[] info = new UTF8Encoding(true).GetBytes(String.Empty);
                            fs.Write(info, 0, info.Length);
                        }
                    }

                    // Check if file is fit for processing
                    string fileContent = File.ReadAllText(file.FullName);
                    bool fileIsFinished = fileContent.Trim().EndsWith("<EOF>");
                    bool fileTooOld = (file.LastWriteTime.AddMinutes(10) < DateTime.Now);
                    if (fileIsFinished || fileTooOld)
                    {
                        // Process file
                        File.AppendAllText(logFile, fileContent.TrimEnd().Replace("<EOF>", String.Empty));

                        // Remove processed file
                        File.Delete(file.FullName);
                    }
                    else
                    {
                        // Stop further processing of files of same type
                        if (isOutput)
                        {
                            outputStopped = true;
                        }

                        if (isError)
                        {
                            errorStopped = true;
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

        public static bool ReadConfigBool(string argName, bool ifNotSet)
        {
            string value = ReadConfig(argName);
            bool isSet = (value == String.Empty ? false : true);
            return (isSet ? Convert.ToBoolean(value) : ifNotSet);
        }

        public static DateTime ReadConfigTime(string argName, DateTime ifNotSet)
        {
            string value = ReadConfig(argName);
            bool isSet = (value == String.Empty ? false : true);
            return (isSet ? DateTime.ParseExact(ReadConfig(argName), "yyyy.MM.dd HH:mm:ss", null) : ifNotSet);
        }

        public static string ReadConfigString(string argName, string ifNotSet)
        {
            string value = ReadConfig(argName);
            bool isSet = (value == String.Empty ? false : true);
            return (isSet ? value : ifNotSet);
        }

        public static int ReadConfigInt(string argName, int ifNotSet)
        {
            string value = ReadConfig(argName);
            bool isSet = (value == String.Empty ? false : true);
            return (isSet ? Convert.ToInt32(value) : ifNotSet);
        }

        public static string ReadConfig(string argName)
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

        public void WriteConfig()
        {
            // Generate configuration file contents
            string cfgText = String.Empty;
            foreach (FieldInfo fieldInfo in configuration.GetType().GetFields())
            {
                cfgText += fieldInfo.Name + "=" + fieldInfo.GetValue(configuration).ToString().ToLower() + Environment.NewLine;
            }
            File.WriteAllText(confFile, cfgText);
        }

        private static void GetStackCallers()
        {
            StackTrace stackTrace = new StackTrace();
            string[] callers = new string[] { };
            for (int i = 0; ; i++)
            {
                if (stackTrace.GetFrame(i).GetILOffset() == StackFrame.OFFSET_UNKNOWN)
                {
                    break;
                }

                var reflectedType = stackTrace.GetFrame(i).GetMethod().ReflectedType;
                if (reflectedType != null)
                {
                    string className = reflectedType.Name;
                    string methodName = stackTrace.GetFrame(i).GetMethod().Name;
                    if (methodName.StartsWith(".") || methodName == stackTrace.GetFrame(0).GetMethod().Name)
                    {
                        continue;
                    }

                    Array.Resize(ref callers, callers.Length + 1);
                    callers[callers.Length - 1] = className + "." + methodName;
                }
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
                string errorMsg = String.Empty;

                try
                {
                    string hint = String.Empty;

                    switch (result)
                    {
                        case 1219:
                            hint = " Hint: If you use same connection within windows, this error will occure. If you use IP in windows, try using host in this connection and vice versa.";
                            break;
                        case 1312:
                            hint = " Hint: Windows is caching credentials after NFS connection that are no longer valid. Running tasks with NFS too often result in this error, once in a 30 minutes seems to be fine.";
                            break;
                    }

                    errorMsg = new Win32Exception(result).Message + ". (Code: " + result + ")" + hint;
                }
                catch (Exception ex)
                {
                    errorMsg = "Error connecting to remote share with unknown code " + result + ".";
                }

                throw new Win32Exception(result, errorMsg);
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

        public enum ResourceScope
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        };

        public enum ResourceType
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8,
        }

        public enum ResourceDisplaytype
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
