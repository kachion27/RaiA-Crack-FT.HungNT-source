using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace rikkei_education_service;

public class VersionChecker : IDisposable
{
	private readonly string versionFilePath;

	private readonly string apiEndpoint = "https://api.raia.edu.vn/api/rikkei-education-app/find-app-info";

	private readonly HttpClient httpClient = new HttpClient();

	private bool disposed = false;

	public VersionChecker()
	{
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		versionFilePath = Path.Combine(baseDirectory, "version.json");
	}

	public async Task<bool> CheckAndUpdateVersionAsync()
	{
		try
		{
			string currentVersion = await GetCurrentVersionAsync();
			if (string.IsNullOrEmpty(currentVersion))
			{
				return false;
			}
			string latestVersion = await GetLatestVersionAsync();
			if (string.IsNullOrEmpty(latestVersion))
			{
				return false;
			}
			if (currentVersion != latestVersion)
			{
				await ShowUpdateNotificationAndUpdateAsync();
				return true;
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private async Task<string> GetCurrentVersionAsync()
	{
		try
		{
			if (!File.Exists(versionFilePath))
			{
				return null;
			}
			return JsonSerializer.Deserialize<VersionData>(await File.ReadAllTextAsync(versionFilePath))?.version;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private async Task<string> GetLatestVersionAsync()
	{
		try
		{
			HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);
			if (response.IsSuccessStatusCode)
			{
				return JsonSerializer.Deserialize<AppInfoResponse>(await response.Content.ReadAsStringAsync())?.windowVersion;
			}
			return null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private async Task ShowUpdateNotificationAndUpdateAsync()
	{
		try
		{
			MessageBox.Show("\ud83d\udd04 Phiên bản hiện tại đã cũ!\n\nỨng dụng sẽ tự động khởi chạy trình cập nhật.\nVui lòng chờ trong giây lát, và cập nhật ứng dụng...", "Cập Nhật Ứng Dụng", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string updateExePath = Path.Combine(appDirectory, "update.exe");
			if (File.Exists(updateExePath))
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					FileName = updateExePath,
					WorkingDirectory = appDirectory,
					UseShellExecute = true
				};
				Process.Start(startInfo);
				await Task.Delay(2000);
				Application.Current.Shutdown();
			}
			else
			{
				MessageBox.Show("❌ Không tìm thấy file update.exe!\n\nVui lòng tải phiên bản mới nhất từ trang chủ.", "Lỗi Cập Nhật", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			MessageBox.Show("❌ Lỗi khi cập nhật: " + ex2.Message, "Lỗi Cập Nhật", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			if (disposing)
			{
				httpClient?.Dispose();
			}
			disposed = true;
		}
	}
}
