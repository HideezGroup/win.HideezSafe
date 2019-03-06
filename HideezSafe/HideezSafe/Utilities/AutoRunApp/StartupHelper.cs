using System;
using System.Linq;
using Microsoft.Win32;
using System.Windows.Forms;

namespace HideezSafe.Utils
{
    /// <summary>
    /// Provides methods and properties to manage an application in startup list.
    /// </summary>
    public class StartupHelper : IStartupHelper
	{
        /// <summary>
        /// Add app to startup list.
        /// </summary>
        /// <returns>True if application is successfully added to startup.</returns>
        public bool AddToStartup()
		{
			return AddToStartup(AppName, ExecutablePath);
		}

        /// <summary>
        /// Add app to startup list.
        /// </summary>
        /// <param name="appName">App name for registry key.</param>
        /// <returns>True if application is successfully added to startup.</returns>
        public bool AddToStartup(string appName)
		{
			return AddToStartup(appName, ExecutablePath);
		}

        /// <summary>
        /// Add app to startup list.
        /// </summary>
        /// <param name="appName">App name for registry key.</param>
        /// <param name="path">Path to executable file.</param>
        /// <returns>True if application is successfully added to startup.</returns>
        public bool AddToStartup(string appName, string path)
		{
			if (string.IsNullOrEmpty(appName))
				throw new ArgumentException("Value can not be empty.", nameof(appName));

			try
			{
				if (IsWindows8orHigher)
				{
					RemoveFromStartup();
				}
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
				{
					registryKey.SetValue(appName, path);
				}
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

        /// <summary>
        /// Remove app from startup list.
        /// </summary>
        /// <returns>True if application is successfully removed from startup.</returns>
        public bool RemoveFromStartup()
		{
			return RemoveFromStartup(AppName);
		}

        /// <summary>
        /// Remove app from startup list.
        /// </summary>
        /// <param name="appName">App name for registry key.</param>
        /// <returns>True if application is successfully removed from startup.</returns>
        public bool RemoveFromStartup(string appName)
		{
			if (string.IsNullOrEmpty(appName))
				throw new ArgumentException("Value can not be empty.", nameof(appName));

			bool res;
			try
			{
				using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
				{
					if (registryKey != null)
					{
						registryKey.DeleteValue(appName);
					}
				}
				res = true;
			}
			catch (Exception)
			{
				res = false;
			}

			try
			{
				if (IsWindows8orHigher)
				{
					// values for Task Manager
					using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", true))
					{
						if (registryKey != null)
						{
							registryKey.DeleteValue(appName);
						}
					}
				}
				res = true;
			}
			catch (Exception)
			{
				if (!res)
				{
					res = false;
				}
			}
			return res;
		}

        /// <summary>
        /// Check if app in startup list.
        /// </summary>
        /// <returns>True if application is in startup list.</returns>
        public bool IsInStartup()
		{
			return IsInStartup(AppName);
		}

		/// <summary>
		/// Check is app in startap list
		/// </summary>
		/// <param name="appName">registry key</param>
		/// <returns></returns>
		public bool IsInStartup(string appName)
		{
			if (string.IsNullOrEmpty(appName))
				throw new ArgumentException("Value can not be empty.", nameof(appName));

			bool isInStartup = false;

			// windows 8 or higher
			if (IsWindows8orHigher)
			{
				try
				{
					using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
					{
						object res = registryKey.GetValue(appName);
						if (res == null)
						{
							isInStartup = false;
						}
						else
						{
							// values for Task Manager
							using (RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run"))
							{
								byte[] data = (byte[])rk.GetValue(appName);
								if (data == null)
								{
									isInStartup = true;
								}
								else if (data[0] == 2 || data.Sum(v => v) == 0)
								{
									isInStartup = true;
								}
								else
								{
									isInStartup = false;
								}
							}
						}
					}
				}
				catch (Exception)
				{
				}
			}
			else
			{
				try
				{
					using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
					{
						object value = registryKey.GetValue(appName);
						if (value == null)
						{
							isInStartup = false;
						}
						else
						{
							isInStartup = true;
						}
					}
				}
				catch (Exception)
				{
				}
			}

			return isInStartup;
		}

        /// <summary>
        /// Gets the name associated with this application.
        /// </summary>
        private string AppName
		{
			get
			{
				return Application.ProductName;
			}
		}

        /// <summary>
        /// Gets the path for the executable file that started the application, including the executable name.
        /// </summary>
        private string ExecutablePath
		{
			get
			{
				return Application.ExecutablePath;
			}
		}

        /// <summary>
        /// True if version of windows 8 or higher.
        /// </summary>
		private bool IsWindows8orHigher
		{
			get
			{
				Version version = Environment.OSVersion.Version;

				// versions of windows
				// 5.1 Windows Xp 32x
				// 5.2 Windows Xp 64x
				// 6.0 Windows Vista
				// 6.1 Windows 7
				// 6.2 Windows 8

				if (version.Major == 6 && version.Minor >= 2 || version.Major == 10)
					return true;
				else
					return false;
			}
		}
	}
}