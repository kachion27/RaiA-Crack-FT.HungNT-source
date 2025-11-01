using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace rikkei_education_service;

public class App : Application
{
	private static Mutex mutex;

	private const string APP_MUTEX_NAME = "RikkeiEducationService_SingleInstance_Mutex";

	protected override async void OnStartup(StartupEventArgs e)
	{
		mutex = new Mutex(initiallyOwned: true, "RikkeiEducationService_SingleInstance_Mutex", out var createdNew);
		if (!createdNew)
		{
			MessageBox.Show("Ứng dụng Rikkei Education Service đã đang chạy!\n\nChỉ có thể mở một instance duy nhất tại một thời điểm.", "Ứng dụng đã chạy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			Environment.Exit(0);
			return;
		}
		base.OnStartup(e);
		await CheckVersionAndUpdateAsync();
		string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		Path.Combine(documentsPath, "RikkeiEducationService", "terms_of_use_accept.json");
		string studentDataPath = Path.Combine(documentsPath, "RikkeiEducationService", "student.json");
		if (File.Exists(studentDataPath))
		{
			MainWindow mainWindow = new MainWindow();
			mainWindow.Show();
		}
		else
		{
			LoginWindow loginWindow = new LoginWindow();
			loginWindow.Show();
		}
	}

	private async Task CheckVersionAndUpdateAsync()
	{
		try
		{
			using VersionChecker versionChecker = new VersionChecker();
			if (await versionChecker.CheckAndUpdateVersionAsync())
			{
				return;
			}
		}
		catch (Exception)
		{
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		mutex?.ReleaseMutex();
		mutex?.Dispose();
		base.OnExit(e);
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "9.0.9.0")]
	public static void Main()
	{
		App app = new App();
		app.Run();
	}
}
