using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;

namespace rikkei_education_service;

public partial class MainWindow : Window, IComponentConnector
{
	private NotifyIcon notifyIcon;

	public Student student;

	private string studentDataPath;

	private MainSocket mainSocket;

	public int examId;

	public string roomType = "CLASS";

	private readonly bool devType = false;

	private string version = "1.4.1";

	private string os = "Windows";

	private StudentSocket studentSocket;

	private LockScreen currentLockScreen;

	public HttpProxyServer proxyServer;

	public ProxySettingsManager proxyManager;

	public AiDetect aiDetect;

	public MainWindow()
	{
		Environment.SetEnvironmentVariable("VIOLATE_HOST", devType ? "http://127.0.0.1:3000/api" : "https://api.raia.edu.vn/api");
		Environment.SetEnvironmentVariable("WS_HOST", devType ? "ws://localhost:3000" : "https://api.raia.edu.vn");
		Environment.SetEnvironmentVariable("PORTAl_URL", devType ? "http://localhost:5173/rikkei-education-auth" : "https://portal.rikkei.edu.vn/rikkei-education-auth");
		InitializeComponent();
		InitializeNotifyIcon();
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		studentDataPath = Path.Combine(folderPath, "RikkeiEducationService", "student.json");
		base.StateChanged += MainWindow_StateChanged;
		base.Closing += MainWindow_Closing;
		base.Loaded += MainWindow_Loaded;
	}

	private void InitializeNotifyIcon()
	{
		notifyIcon = new NotifyIcon();
		string fileName = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Images", "icon", "icon.ico");
		notifyIcon.Icon = new Icon(fileName);
		notifyIcon.Text = "Rikkei Education Service";
		notifyIcon.Visible = false;
		notifyIcon.DoubleClick += delegate
		{
			ShowWindow();
		};
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add("Show", null, delegate
		{
			ShowWindow();
		});
		contextMenuStrip.Items.Add("Exit", null, delegate
		{
			RestoreProxy();
			System.Windows.Application.Current.Shutdown();
		});
		notifyIcon.ContextMenuStrip = contextMenuStrip;
	}

	private void ShowWindow()
	{
		Show();
		base.WindowState = WindowState.Normal;
		notifyIcon.Visible = false;
	}

	public void ShowBalloonTip(string title, string message)
	{
		notifyIcon.ShowBalloonTip(1500, title, message, ToolTipIcon.Info);
	}

	private void MainWindow_StateChanged(object sender, EventArgs e)
	{
		if (base.WindowState == WindowState.Minimized)
		{
			Hide();
			notifyIcon.Visible = true;
			ShowBalloonTip("Rikkei Education Service", "Ứng dụng đã được thu nhỏ vào khay hệ thống.");
		}
	}

	private void MainWindow_Closing(object sender, CancelEventArgs e)
	{
		e.Cancel = true;
		Hide();
		notifyIcon.Visible = true;
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		LoadStudentData();
		InitializeSocket();
		VersionText.Text = version ?? "";
		sendToWifiData();
	}

	public void sendToWifiData()
	{
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = "netsh",
			Arguments = "wlan show interfaces",
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		Process process = Process.Start(startInfo);
		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		string text = ExtractWifiInfo(output, "SSID");
		string text2 = ExtractWifiInfo(output, "AP BSSID");
	}

	private string ExtractWifiInfo(string output, string key)
	{
		try
		{
			string[] array = output.Split('\n');
			string[] array2 = array;
			foreach (string text in array2)
			{
				string text2 = text.Trim();
				if (text2.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase))
				{
					int num = text2.IndexOf(':');
					if (num > 0 && num < text2.Length - 1)
					{
						return text2.Substring(num + 1).Trim();
					}
				}
			}
			return "N/A";
		}
		catch (Exception)
		{
			return "N/A";
		}
	}

	public void LockScreen()
	{
		try
		{
			if (currentLockScreen == null || !currentLockScreen.IsVisible)
			{
				currentLockScreen = new LockScreen();
				currentLockScreen.Show();
				Hide();
				UpdateApplicationStatus("Màn hình đã bị khóa");
				ShowBalloonTip("Lock Screen", "Màn hình đã bị khóa bởi giám thị!");
			}
		}
		catch (Exception)
		{
		}
	}

	public void UnlockScreen()
	{
		try
		{
			if (currentLockScreen != null && currentLockScreen.IsVisible)
			{
				currentLockScreen.Unlock();
				currentLockScreen = null;
				Show();
				base.WindowState = WindowState.Normal;
				UpdateApplicationStatus("Màn hình đã được mở khóa");
				ShowBalloonTip("Unlock Screen", "Màn hình đã được mở khóa!");
			}
		}
		catch (Exception)
		{
		}
	}

	public void StartProxyServer()
	{
		try
		{
			proxyServer = new HttpProxyServer(this);
			proxyServer.Start();
			proxyManager = new ProxySettingsManager();
			proxyManager.EnableSystemProxy();
			ShowBalloonTip("Proxy Active", "System proxy đã được kích hoạt!\nTất cả traffic sẽ đi qua proxy.");
			ProxyStatus currentProxyStatus = proxyManager.GetCurrentProxyStatus();
			ProxyStatusButton.Content = "ONLINE";
			ProxyStatusButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 235, 45));
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Không thể kích hoạt proxy: " + ex.Message + "\n\nVui lòng chạy app với quyền Administrator.", "Lỗi Proxy", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public void StartLoadAiDetect()
	{
		aiDetect = new AiDetect();
		aiDetect.SetMainWindow(this);
		aiDetect.StartMonitoring();
	}

	private void RestoreProxyButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (proxyServer != null)
			{
				proxyServer.Stop();
				proxyServer = null;
			}
			if (proxyManager != null)
			{
				proxyManager.DisableSystemProxy();
			}
			else
			{
				ProxySettingsManager proxySettingsManager = new ProxySettingsManager();
				proxySettingsManager.DisableSystemProxy();
			}
			ProxyStatusButton.Content = "OFFLINE";
			ProxyStatusButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
			ShowBalloonTip("Proxy Restored", "Proxy đã được tắt!\nInternet sẽ hoạt động bình thường.");
			System.Windows.MessageBox.Show("✅ Proxy đã được tắt thành công!\n\nInternet sẽ hoạt động bình thường.\n\nNếu muốn bật lại proxy, hãy restart app.", "Khôi Phục Thành Công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("❌ Lỗi khi khôi phục proxy: " + ex.Message + "\n\nVui lòng restart app hoặc kiểm tra quyền Administrator.", "Lỗi Khôi Phục", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public void RestoreProxy()
	{
		try
		{
			if (proxyServer != null)
			{
				proxyServer.Stop();
				proxyServer = null;
			}
			if (proxyManager != null)
			{
				proxyManager.DisableSystemProxy();
			}
			else
			{
				ProxySettingsManager proxySettingsManager = new ProxySettingsManager();
				proxySettingsManager.DisableSystemProxy();
			}
			ProxyStatusButton.Content = "OFFLINE";
			ProxyStatusButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
			ShowBalloonTip("Proxy Restored", "Proxy đã được tắt!\nInternet sẽ hoạt động bình thường.");
		}
		catch (Exception)
		{
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		try
		{
			if (proxyServer != null)
			{
				proxyServer.Stop();
			}
			if (proxyManager != null)
			{
				proxyManager.DisableSystemProxy();
			}
		}
		catch (Exception)
		{
		}
		base.OnClosed(e);
	}

	private void InitializeSocket()
	{
		mainSocket = new MainSocket();
		mainSocket.ConnectionStatusChanged += OnSocketConnectionChanged;
		studentSocket = new StudentSocket(this, mainSocket);
		mainSocket.ConnectAsync();
	}

	private void OnSocketConnectionChanged(object sender, bool isConnected)
	{
		base.Dispatcher.Invoke(delegate
		{
			if (isConnected)
			{
				UpdateStudentWifiInfo();
				mainSocket.SendObjectAsync("auth", student);
				SocketStatusButton.Content = "ONLINE";
				SocketStatusButton.Background = System.Windows.Media.Brushes.Green;
				ShowBalloonTip("Rikkei Education Service", "Đã kết nối!");
			}
			else
			{
				SocketStatusButton.Content = "OFFLINE";
				SocketStatusButton.Background = System.Windows.Media.Brushes.Red;
				ShowBalloonTip("Rikkei Education Service", "Đã ngắt kết nối!");
			}
		});
	}

	private void UpdateStudentWifiInfo()
	{
		try
		{
			if (student != null)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					FileName = "netsh",
					Arguments = "wlan show interfaces",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};
				Process process = Process.Start(startInfo);
				string output = process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				student.WifiSSID = ExtractWifiInfo(output, "SSID");
				student.WifiBSSID = ExtractWifiInfo(output, "AP BSSID");
				student.Os = os;
			}
		}
		catch (Exception)
		{
			if (student != null)
			{
				student.WifiSSID = "N/A";
				student.WifiBSSID = "N/A";
				student.Os = os;
			}
		}
	}

	private void LoadStudentData()
	{
		try
		{
			if (File.Exists(studentDataPath))
			{
				string json = File.ReadAllText(studentDataPath);
				student = JsonSerializer.Deserialize<Student>(json);
				if (student != null)
				{
					UpdateStudentUI();
					ShowBalloonTip("Thành công", "Đã tải thông tin sinh viên!");
				}
				else
				{
					System.Windows.MessageBox.Show("Dữ liệu sinh viên không hợp lệ", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					Close();
				}
			}
			else
			{
				System.Windows.MessageBox.Show("Không tìm thấy dữ liệu sinh viên", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				Close();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Lỗi khi tải dữ liệu sinh viên: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			Close();
		}
	}

	private void UpdateStudentUI()
	{
		if (student != null)
		{
			StudentCodeText.Text = student.StudentCode ?? "N/A";
			FullNameText.Text = student.FullName ?? "N/A";
			EmailText.Text = student.Email ?? "N/A";
			PhoneText.Text = student.Phone ?? "N/A";
			DobText.Text = student.GetFormattedDob();
			SystemIdText.Text = student.SystemId.ToString();
			StudentIdText.Text = student.StudentId.ToString();
		}
	}

	public void UpdateApplicationStatus(string newStatus)
	{
		StatusText.Text = newStatus;
		if (newStatus.Contains("Đợi Lệnh"))
		{
			StatusBorder.Background = System.Windows.Media.Brushes.Blue;
		}
		else
		{
			StatusBorder.Background = System.Windows.Media.Brushes.Green;
		}
	}

	private void LogoutButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?\n\nTất cả dữ liệu sinh viên sẽ bị xóa.", "Xác nhận đăng xuất", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (messageBoxResult == MessageBoxResult.Yes)
			{
				RestoreProxy();
				if (File.Exists(studentDataPath))
				{
					File.Delete(studentDataPath);
				}
				Close();
				LoginWindow loginWindow = new LoginWindow();
				loginWindow.Show();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Lỗi khi đăng xuất: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}
}
