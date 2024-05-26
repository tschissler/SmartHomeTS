using nanoFramework.Hardware.Esp32;
using nanoFramework.Json;
using nanoFramework.Networking;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace KellerDevice
{
    public class Program
    {
        const string RootPath = "I:\\";
        const string Version = RootPath + "version";
        const string CodeRunning = "Code Running";
        const string CodeNotRunning = "Code NOT Running";
        const string IntegrityError = "Integrity error";
        const string OtaRunnerName = "CountMeasurement.OtaRunner";

        static int sleepTimeMinutes = 60000;
        static int millisecondsToSleep = 500;
        static int version = -1;
        static bool isRunning = false;
        static Assembly toRun = null;
        static MethodInfo stop = null;
        static HttpClient httpClient = new HttpClient();
        static FileSettings[] filesToDownload = null;

        public static void Main()
        {
            try
            {
                Trace("Program Started.");

                Trace("Connecting to wifi.");
                // As we are using TLS, we need a valid date & time
                // We will wait maximum 1 minute to get connected and have a valid date
                CancellationTokenSource cs = new(sleepTimeMinutes);
                var success = WifiNetworkHelper.ConnectDhcp(Secrets.Ssid, Secrets.Password, requiresDateTime: true, token: cs.Token);
                if (!success)
                {
                    Trace($"Can't connect to wifi: {WifiNetworkHelper.Status}");
                    if (WifiNetworkHelper.HelperException != null)
                    {
                        Trace($"{WifiNetworkHelper.HelperException}");
                    }

                    GoToSleep();
                }

                Trace($"Date and time is now {DateTime.UtcNow}");

                // First let's check if we have a version
                if (File.Exists(Version))
                {
                    using FileStream fs = new FileStream(Version, FileMode.Open, FileAccess.Read);
                    byte[] buff = new byte[fs.Length];
                    fs.Read(buff, 0, buff.Length);
                    // This if the version we have stored
                    version = BinaryPrimitives.ReadInt32BigEndian(buff);
                }


                    ProcessTwinAndDownloadFiles(3);

            }
            catch (Exception ex)
            {
                // We won't do anything
                // This global try catch is to make sure whatever happen, we will safely be able to go
                // To sleep
                Trace(ex.ToString());
                GoToSleep();
            }

            Thread.Sleep(Timeout.InfiniteTimeSpan);

            Thread.Sleep(Timeout.Infinite);
        }
        static void Trace(string message)
        {
            Debug.WriteLine(message);
        }

        static void GoToSleep()
        {
            Trace($"Set wakeup by timer for {millisecondsToSleep} minutes to retry.");
            Sleep.EnableWakeupByTimer(new TimeSpan(0, 0, millisecondsToSleep, 0));
            Trace("Deep sleep now");
            Sleep.StartDeepSleep();
        }

        static void ProcessTwinAndDownloadFiles(int desiredVersion)
        {
            int codeVersion = 0;
            codeVersion = 3;
            string[] files;
            // If the version is the same as the stored one, no changes, we can load the code
            // Otherwise we have to download a new version
            if (codeVersion != version)
            {
                // Stop the previous instance
                stop?.Invoke(null, null);
                // Let's first clean all the pe files
                // We keep any other file
                files = Directory.GetFiles(RootPath);
                foreach (var file in files)
                {
                    if (file.EndsWith(".pe"))
                    {
                        File.Delete(file);
                    }
                }

                // Now download all the files from the twin
                string token = (string)desired["Token"];
                var desiredFiles = desired["Files"] as ArrayList;
                filesToDownload = new FileSettings[desiredFiles.Count];
                int inc = 0;
                foreach (var singleFile in desiredFiles)
                {
                    FileSettings file = (FileSettings)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(singleFile), typeof(FileSettings));
                    filesToDownload[inc++] = file;
                    DownloadBinaryFile(file.FileName, token);
                }

                using FileStream fs = new FileStream(Version, FileMode.Create, FileAccess.Write);
                byte[] buff = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(buff, codeVersion);
                fs.Write(buff, 0, buff.Length);
                fs.Flush();
                fs.Dispose();

                // If we had a previous instance, we will reboot
                // this will allow to have the previous code cleared
                if (isRunning)
                {
                    // We go to sleep and we don't reboot
                    // Going to sleep will clean as well native part
                    GoToSleep();
                }
            }

            // Now load the assemblies, they must be on the disk
            if (!isRunning)
            {
                LoadAssemblies();

                isRunning = false;
                if (toRun != null)
                {
                    Type typeToRun = toRun.GetType(OtaRunnerName);
                    var start = typeToRun.GetMethod("Start");
                    stop = typeToRun.GetMethod("Stop");

                    if (start != null)
                    {
                        try
                        {
                            // See if all goes right
                            start.Invoke(null, new object[] { });
                            isRunning = true;
                        }
                        catch (Exception)
                        {
                            isRunning = false;
                        }
                    }
                }
            }
        }

        static void DownloadBinaryFile(string url, string sas)
        {
            string fileName = url.Substring(url.LastIndexOf('/') + 1);

            httpClient.DefaultRequestHeaders.Add("x-ms-blob-type", "BlockBlob");
            // this example uses Tls 1.2 with Azure
            httpClient.SslProtocols = System.Net.Security.SslProtocols.Tls12;
            // use the pem certificate we created earlier
            httpClient.HttpsAuthentCert = new X509Certificate(Resource.GetString(Resource.StringResources.AzureRootCerts));
            HttpResponseMessage response = httpClient.Get($"{url}?{sas}");
            response.EnsureSuccessStatusCode();

            using FileStream fs = new FileStream($"{RootPath}{fileName}", FileMode.Create, FileAccess.Write);
            response.Content.ReadAsStream().CopyTo(fs);
            fs.Flush();
            fs.Close();
            response.Dispose();
        }

        static void LoadAssemblies()
        {
            // Now load all the assemblies we have on the storage
            var files = Directory.GetFiles(RootPath);
            foreach (var file in files)
            {
                if (file.EndsWith(".pe"))
                {
                    using FileStream fspe = new FileStream(file, FileMode.Open, FileAccess.Read);
                    Trace($"{file}: {fspe.Length}");
                    var buff = new byte[fspe.Length];
                    fspe.Read(buff, 0, buff.Length);
                    // Needed as so far, there seems to be an issue when loading them too fast
                    fspe.Close();
                    fspe.Dispose();
                    Thread.Sleep(20);
                    bool integrity = true;
                    string strsha = string.Empty;
                    // Check integrity if we just downloaded it
                    if (filesToDownload != null)
                    {
                        integrity = false;
                        var sha256 = SHA256.Create().ComputeHash(buff);
                        strsha = BitConverter.ToString(sha256);
                        var fileName = file.Substring(file.LastIndexOf('\\') + 1);
                        foreach (FileSettings filesetting in filesToDownload)
                        {
                            if (filesetting.FileName.Substring(filesetting.FileName.LastIndexOf('/') + 1) == fileName)
                            {
                                if (strsha == filesetting.Signature)
                                {
                                    integrity = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!integrity)
                    {
                        Trace("Error with file signature");
                        break;
                    }

                    var ass = Assembly.Load(buff);
                    var typeToRun = ass.GetType(OtaRunnerName);
                    if (typeToRun != null)
                    {
                        toRun = ass;
                    }
                }
            }
        }
    }
}
