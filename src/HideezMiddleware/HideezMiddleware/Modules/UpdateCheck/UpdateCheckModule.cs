using Hideez.SDK.Communication.Log;
using HideezMiddleware.IPC.Messages;
using HideezMiddleware.Modules.UpdateCheck.Messages;
using Meta.Lib.Modules.PubSub;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace HideezMiddleware.Modules.UpdateCheck
{
    public sealed class UpdateCheckModule : ModuleBase
    {
        const string REG_UPDATE_ADDRESS = "custom_update_srv_address";

        private readonly Version _currentProductVersion = typeof(UpdateCheckModule).Assembly.GetName().Version;
        private readonly string _updateConfigUrl = "http://update.hideez.com/update/hideezclient/update.xml";
        private readonly RegistryKey _registryKey;
        private readonly string _updateCacheRegistryValueName = "update_cache_path";

        public UpdateCheckModule(RegistryKey registryKey, IMetaPubSub messenger, ILog log)
            : base(messenger, nameof(UpdateCheckModule), log)
        {
            _registryKey = registryKey;

            ClearOutdatedCache();

            Task.Run(RunUpdateCheck);
        }

        // Delete installed or outdated update from local cache
        private void ClearOutdatedCache()
        {
            try
            {
                var cachedUpdate = GetCachedUpdateInfo();
                if (cachedUpdate != null && _currentProductVersion > cachedUpdate.Version)
                    DeleteCachedUpdate();
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        private async Task RunUpdateCheck()
        {
            try
            {
                var availableUpdateInfo = GetAvailableAppUpdateInfo();
                var cachedUpdateInfo = GetCachedUpdateInfo();

                if (availableUpdateInfo != null && availableUpdateInfo.Version > _currentProductVersion)
                {
                    if (cachedUpdateInfo == null || availableUpdateInfo.Version > cachedUpdateInfo.Version)
                    {
                        DeleteCachedUpdate();
                        DownloadUpdate(availableUpdateInfo);
                    }
                }

                await CheckCachedUpdate();
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

            _messenger.Subscribe(GetSafeHandler<LoginClientRequestMessage>(OnServiceClientLogin));
        }

        /// <summary>
        /// Returns <see cref="AppUpdateInfo"/> populated by data from update server
        /// </summary>
        private AppUpdateInfo GetAvailableAppUpdateInfo()
        {
            try
            {
                string updateConfigUrl = GetUpdateUrl();
                WebRequest webRequest = WebRequest.Create(updateConfigUrl);
                WebResponse webResponse = webRequest.GetResponse();

                var receivedAppCastDocument = new XmlDocument();
                using (Stream dataStream = webResponse.GetResponseStream())
                {
                    receivedAppCastDocument.Load(dataStream);
                }

                XmlNodeList appCastItems = receivedAppCastDocument.SelectNodes("item");

                var appUpdateInfo = new AppUpdateInfo();
                foreach (XmlNode item in appCastItems)
                {
                    appUpdateInfo.Version = new Version(item.SelectSingleNode("version")?.InnerText);
                    appUpdateInfo.Changelog = item.SelectSingleNode("changelog")?.InnerText;
                    appUpdateInfo.Url = item.SelectSingleNode("url")?.InnerText;
                    break; // only one 'item' node per file is supported
                }

                return appUpdateInfo;
            }
            catch (Exception)
            {
                // Any number of errors may prevent us from getting an update config
                return null;
            }
        }

        // Returns custom url to update if it exists
        string GetUpdateUrl()
        {
            var regUrl = _registryKey.GetValue(REG_UPDATE_ADDRESS) as string;
            if (!string.IsNullOrWhiteSpace(regUrl))
                return regUrl;
            else
                return _updateConfigUrl;
        }

        /// <summary>
        /// Returns <see cref="CachedUpdateInfo"/> populated by data from local cache
        /// </summary>
        private CachedUpdateInfo GetCachedUpdateInfo()
        {
            try
            {
                string filepath = _registryKey.GetValue(_updateCacheRegistryValueName) as string;
                if (!string.IsNullOrWhiteSpace(filepath) && File.Exists(filepath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(filepath);
                    var version = new Version(versionInfo.FileVersion);

                    return new CachedUpdateInfo(filepath, version);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }

            return null;
        }

        // Delete update file and registry reference to its path
        private void DeleteCachedUpdate()
        {
            string filepath = _registryKey.GetValue(_updateCacheRegistryValueName) as string;
            if (!string.IsNullOrWhiteSpace(filepath) && File.Exists(filepath))
            {
                File.Delete(filepath);
                
                var dir = Path.GetDirectoryName(filepath);
                if (dir != Path.GetTempPath())
                    Directory.Delete(dir);

                _registryKey.DeleteValue(_updateCacheRegistryValueName);
            }
        }

        // Download update file to temporary folder and store a path to it in registry
        private bool DownloadUpdate(AppUpdateInfo updateInfo)
        {
            try
            {
                var uri = new Uri(updateInfo.Url);
                string filename = Path.GetFileName(uri.LocalPath);
                string folderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", ""));
                string targetPath = Path.Combine(folderPath, filename);

                byte[] downloadedData;

                downloadedData = new byte[0];

                //open a data stream from the supplied URL
                WebRequest webReq = WebRequest.Create(updateInfo.Url);
                WebResponse webResponse = webReq.GetResponse();

                //Download the data in chuncks
                byte[] dataBuffer = new byte[1024];

                //Get the total size of the download
                int dataLength = (int)webResponse.ContentLength;

                //Download the data
                using (Stream dataStream = webResponse.GetResponseStream())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        while (true)
                        {
                            //Let's try and read the data
                            int bytesFromStream = dataStream.Read(dataBuffer, 0, dataBuffer.Length);

                            if (bytesFromStream == 0)
                            {
                                //Download complete
                                break;
                            }
                            else
                            {
                                //Write the downloaded data
                                ms.Write(dataBuffer, 0, bytesFromStream);
                            }
                        }


                        //Convert the downloaded stream to a byte array
                        downloadedData = ms.ToArray();
                    }
                }

                // Todo: Hashsum check for downloaded update exe file

                // Ensure that target path is available
                Directory.CreateDirectory(folderPath);
                File.Delete(targetPath);

                //Write bytes to the specified file
                using (FileStream fs = new FileStream(targetPath, FileMode.Create))
                {
                    fs.Write(downloadedData, 0, downloadedData.Length);
                }

                // Save path to downloaded file in registry
                _registryKey.SetValue(_updateCacheRegistryValueName, targetPath, RegistryValueKind.String);

                return true;
            }

            catch (Exception)
            {
                //We may not be connected to the internet
                //Or the URL may be incorrect
                return false;
            }
        }

        // Check cache for possible update and publish a message if cached update is available
        private async Task CheckCachedUpdate()
        {
            var cachedUpdateInfo = GetCachedUpdateInfo();
            if (cachedUpdateInfo != null && cachedUpdateInfo.Version > _currentProductVersion)
                await SafePublish(new ApplicationUpdateAvailableMessage(cachedUpdateInfo.Filepath, cachedUpdateInfo.Version.ToString()));
        }

        // Run cache check when client connects to service
        private async Task OnServiceClientLogin(LoginClientRequestMessage arg)
        {
            await CheckCachedUpdate();
        }
    }
}
