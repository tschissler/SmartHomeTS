using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace KellerDevice
{
    public class Program
    {
        const string RootPath = "I:\\";
        const int wifiTImeoutMilliseconds = 60000;

        static HttpClient httpClient = null;

        public static void Main()
        {
            try
            {
                Logger.LogInformation("Program Started.");

                var wifi = new WifiConnector();

                while (!wifi.IsConnected)
                {
                    Thread.Sleep(1000);
                }

                Logger.LogInformation($"Date and time is now {DateTime.UtcNow}");

                httpClient = new HttpClient();
                DownloadBinaryFile("https://iotstoragem1.blob.core.windows.net/firmwareupdates/TemperatureSensorFirmware_0.0.1.bin");
            }
            catch (Exception ex)
            {
                // We won't do anything
                // This global try catch is to make sure whatever happen, we will safely be able to go
                // To sleep
                Logger.LogError(ex.ToString());
                //GoToSleep();
            }


            Thread.Sleep(Timeout.Infinite);
        }

        //static void GoToSleep()
        //{
        //    Trace($"Set wakeup by timer for {millisecondsToSleep} minutes to retry.");
        //    Sleep.EnableWakeupByTimer(new TimeSpan(0, 0, millisecondsToSleep, 0));
        //    Trace("Deep sleep now");
        //    Sleep.StartDeepSleep();
        //}

        //static void ProcessTwinAndDownloadFiles(int desiredVersion)
        //{
        //    int codeVersion = 0;
        //    codeVersion = 3;
        //    string[] files;
        //    // If the version is the same as the stored one, no changes, we can load the code
        //    // Otherwise we have to download a new version
        //    if (codeVersion != version)
        //    {
        //        // Stop the previous instance
        //        stop?.Invoke(null, null);
        //        // Let's first clean all the pe files
        //        // We keep any other file
        //        files = Directory.GetFiles(RootPath);
        //        foreach (var file in files)
        //        {
        //            if (file.EndsWith(".pe"))
        //            {
        //                File.Delete(file);
        //            }
        //        }

        //        // Now download all the files from the twin
        //        string token = (string)desired["Token"];
        //        var desiredFiles = desired["Files"] as ArrayList;
        //        filesToDownload = new FileSettings[desiredFiles.Count];
        //        int inc = 0;
        //        foreach (var singleFile in desiredFiles)
        //        {
        //            FileSettings file = (FileSettings)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(singleFile), typeof(FileSettings));
        //            filesToDownload[inc++] = file;
        //            DownloadBinaryFile(file.FileName, token);
        //        }

        //        using FileStream fs = new FileStream(Version, FileMode.Create, FileAccess.Write);
        //        byte[] buff = new byte[4];
        //        BinaryPrimitives.WriteInt32BigEndian(buff, codeVersion);
        //        fs.Write(buff, 0, buff.Length);
        //        fs.Flush();
        //        fs.Dispose();

        //        // If we had a previous instance, we will reboot
        //        // this will allow to have the previous code cleared
        //        if (isRunning)
        //        {
        //            // We go to sleep and we don't reboot
        //            // Going to sleep will clean as well native part
        //            GoToSleep();
        //        }
        //    }

        //    // Now load the assemblies, they must be on the disk
        //    if (!isRunning)
        //    {
        //        LoadAssemblies();

        //        isRunning = false;
        //        if (toRun != null)
        //        {
        //            Type typeToRun = toRun.GetType(OtaRunnerName);
        //            var start = typeToRun.GetMethod("Start");
        //            stop = typeToRun.GetMethod("Stop");

        //            if (start != null)
        //            {
        //                try
        //                {
        //                    // See if all goes right
        //                    start.Invoke(null, new object[] { });
        //                    isRunning = true;
        //                }
        //                catch (Exception)
        //                {
        //                    isRunning = false;
        //                }
        //            }
        //        }
        //    }
        //}

        static void DownloadBinaryFile(string url)
        {
            string fileName = url.Substring(url.LastIndexOf('/') + 1);

            httpClient.DefaultRequestHeaders.Add("x-ms-blob-type", "BlockBlob");
            // this example uses Tls 1.2 with Azure
            httpClient.SslProtocols = System.Net.Security.SslProtocols.Tls12;
            string cert = @"-----BEGIN CERTIFICATE-----
MIIDdzCCAl+gAwIBAgIEAgAAuTANBgkqhkiG9w0BAQUFADBaMQswCQYDVQQGEwJJ
RTESMBAGA1UEChMJQmFsdGltb3JlMRMwEQYDVQQLEwpDeWJlclRydXN0MSIwIAYD
VQQDExlCYWx0aW1vcmUgQ3liZXJUcnVzdCBSb290MB4XDTAwMDUxMjE4NDYwMFoX
DTI1MDUxMjIzNTkwMFowWjELMAkGA1UEBhMCSUUxEjAQBgNVBAoTCUJhbHRpbW9y
ZTETMBEGA1UECxMKQ3liZXJUcnVzdDEiMCAGA1UEAxMZQmFsdGltb3JlIEN5YmVy
VHJ1c3QgUm9vdDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKMEuyKr
mD1X6CZymrV51Cni4eiVgLGw41uOKymaZN+hXe2wCQVt2yguzmKiYv60iNoS6zjr
IZ3AQSsBUnuId9Mcj8e6uYi1agnnc+gRQKfRzMpijS3ljwumUNKoUMMo6vWrJYeK
mpYcqWe4PwzV9/lSEy/CG9VwcPCPwBLKBsua4dnKM3p31vjsufFoREJIE9LAwqSu
XmD+tqYF/LTdB1kC1FkYmGP1pWPgkAx9XbIGevOF6uvUA65ehD5f/xXtabz5OTZy
dc93Uk3zyZAsuT3lySNTPx8kmCFcB5kpvcY67Oduhjprl3RjM71oGDHweI12v/ye
jl0qhqdNkNwnGjkCAwEAAaNFMEMwHQYDVR0OBBYEFOWdWTCCR1jMrPoIVDaGezq1
BE3wMBIGA1UdEwEB/wQIMAYBAf8CAQMwDgYDVR0PAQH/BAQDAgEGMA0GCSqGSIb3
DQEBBQUAA4IBAQCFDF2O5G9RaEIFoN27TyclhAO992T9Ldcw46QQF+vaKSm2eT92
9hkTI7gQCvlYpNRhcL0EYWoSihfVCr3FvDB81ukMJY2GQE/szKN+OMY3EU/t3Wgx
jkzSswF07r51XgdIGn9w/xZchMB5hbgF/X++ZRGjD8ACtPhSNzkE1akxehi/oCr0
Epn3o0WC4zxe9Z2etciefC7IpJ5OCBRLbf1wbWsaY71k5h+3zvDyny67G7fyUIhz
ksLi4xaNmjICq44Y3ekQEe5+NauQrz4wlHrQMz2nZQ/1/I6eYs9HRCwBXbsdtTLS
R9I4LtD+gdwyah617jzV/OeBHRnDJELqYzmp
-----END CERTIFICATE-----";
            httpClient.HttpsAuthentCert = new X509Certificate(cert);

            HttpResponseMessage response = httpClient.Get(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError($"Failed to download {url} - HTTP Error: {response.StatusCode}");
                response.Dispose();
                return;
            }

            using FileStream fs = new FileStream($"{RootPath}{fileName}", FileMode.Create, FileAccess.Write);
            response.Content.ReadAsStream().CopyTo(fs);
            fs.Flush();
            fs.Close();
            response.Dispose();
         }

            //static void LoadAssemblies()
            //{
            //    // Now load all the assemblies we have on the storage
            //    var files = Directory.GetFiles(RootPath);
            //    foreach (var file in files)
            //    {
            //        if (file.EndsWith(".pe"))
            //        {
            //            using FileStream fspe = new FileStream(file, FileMode.Open, FileAccess.Read);
            //            Trace($"{file}: {fspe.Length}");
            //            var buff = new byte[fspe.Length];
            //            fspe.Read(buff, 0, buff.Length);
            //            // Needed as so far, there seems to be an issue when loading them too fast
            //            fspe.Close();
            //            fspe.Dispose();
            //            Thread.Sleep(20);
            //            bool integrity = true;
            //            string strsha = string.Empty;
            //            // Check integrity if we just downloaded it
            //            if (filesToDownload != null)
            //            {
            //                integrity = false;
            //                var sha256 = SHA256.Create().ComputeHash(buff);
            //                strsha = BitConverter.ToString(sha256);
            //                var fileName = file.Substring(file.LastIndexOf('\\') + 1);
            //                foreach (FileSettings filesetting in filesToDownload)
            //                {
            //                    if (filesetting.FileName.Substring(filesetting.FileName.LastIndexOf('/') + 1) == fileName)
            //                    {
            //                        if (strsha == filesetting.Signature)
            //                        {
            //                            integrity = true;
            //                            break;
            //                        }
            //                    }
            //                }
            //            }

            //            if (!integrity)
            //            {
            //                Trace("Error with file signature");
            //                break;
            //            }

            //            var ass = Assembly.Load(buff);
            //            var typeToRun = ass.GetType(OtaRunnerName);
            //            if (typeToRun != null)
            //            {
            //                toRun = ass;
            //            }
            //        }
            //    }
            //}
        }
    }
