using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace rikkei_education_service;

public class ProxySettingsManager
{
	private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;

	private const int INTERNET_OPTION_REFRESH = 37;

	public void EnableSystemProxy()
	{
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", writable: true))
			{
				if (registryKey == null)
				{
					throw new Exception("Không thể mở Registry key");
				}
				registryKey.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
				registryKey.SetValue("ProxyServer", "0.0.0.0:0", RegistryValueKind.String);
				registryKey.SetValue("ProxyOverride", "<local>", RegistryValueKind.String);
			}
			RefreshSystemSettings();
			Console.WriteLine("");
		}
		catch (Exception ex)
		{
			throw new Exception(" " + ex.Message);
		}
	}

	public void DisableSystemProxy()
	{
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", writable: true))
			{
				if (registryKey != null)
				{
					registryKey.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
					registryKey.SetValue("ProxyServer", "", RegistryValueKind.String);
				}
			}
			RefreshSystemSettings();
		}
		catch (Exception)
		{
		}
	}

	private void RefreshSystemSettings()
	{
		try
		{
			int num = InternetSetOption(IntPtr.Zero, 39, IntPtr.Zero, 0);
			int num2 = InternetSetOption(IntPtr.Zero, 37, IntPtr.Zero, 0);
			if (num == 1 && num2 != 1)
			{
			}
		}
		catch (Exception)
		{
		}
	}

	public ProxyStatus GetCurrentProxyStatus()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
			if (registryKey != null)
			{
				int num = (int)registryKey.GetValue("ProxyEnable", 0);
				string proxyServer = (string)registryKey.GetValue("ProxyServer", "");
				string proxyOverride = (string)registryKey.GetValue("ProxyOverride", "");
				return new ProxyStatus
				{
					IsEnabled = (num == 1),
					ProxyServer = proxyServer,
					ProxyOverride = proxyOverride
				};
			}
		}
		catch (Exception)
		{
		}
		return new ProxyStatus
		{
			IsEnabled = false
		};
	}

	[DllImport("wininet.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int InternetSetOption(nint hInternet, int dwOption, nint lpBuffer, int dwBufferLength);
}
