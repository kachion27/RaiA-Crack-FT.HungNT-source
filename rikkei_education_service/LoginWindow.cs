using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Markup;

namespace rikkei_education_service;

public partial class LoginWindow : Window, IComponentConnector
{
	private HttpListener listener;

	private bool isServerRunning = false;

	private int? port;

	private string portalUrl = Environment.GetEnvironmentVariable("PORTAl_URL");

	public LoginWindow()
	{
		InitializeComponent();
		base.Loaded += MainWindow_Loaded;
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		port = FindRandomPort();
		if (port == 0)
		{
			MessageBox.Show("Không thể tìm thấy cổng trống để khởi động server. Vui lòng thử lại sau.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			Application.Current.Shutdown();
		}
	}

	private int FindRandomPort()
	{
		Random random = new Random();
		for (int i = 0; i < 100; i++)
		{
			int result = random.Next(3000, 65535);
			if (IsPortAvailable(result))
			{
				return result;
			}
		}
		return 0;
	}

	private bool IsPortAvailable(int port)
	{
		try
		{
			TcpClient tcpClient = new TcpClient();
			tcpClient.Connect("localhost", port);
			tcpClient.Close();
			return false;
		}
		catch (SocketException)
		{
			return true;
		}
	}

	private void StartLocalServer()
	{
		try
		{
			listener = new HttpListener();
			listener.Prefixes.Add($"http://localhost:{port}/");
			listener.Start();
			isServerRunning = true;
			ListenForRequests();
		}
		catch (Exception)
		{
			Application.Current.Shutdown();
		}
	}

	private async void ListenForRequests()
	{
		while (isServerRunning)
		{
			try
			{
				ProcessRequest(await listener.GetContextAsync());
			}
			catch (Exception)
			{
			}
		}
	}

	private void AddCorsHeaders(HttpListenerResponse response)
	{
		response.Headers.Add("Access-Control-Allow-Origin", "*");
		response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
		response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
		response.Headers.Add("Access-Control-Allow-Credentials", "true");
		response.Headers.Add("Access-Control-Max-Age", "86400");
	}

	private void ProcessRequest(HttpListenerContext context)
	{
		HttpListenerRequest request = context.Request;
		HttpListenerResponse response = context.Response;
		AddCorsHeaders(response);
		if (request.HttpMethod == "OPTIONS")
		{
			response.StatusCode = 200;
			response.Close();
			return;
		}
		if (request.HttpMethod == "POST")
		{
			using StreamReader streamReader = new StreamReader(request.InputStream);
			string json = streamReader.ReadToEnd();
			try
			{
				Student studentData = JsonSerializer.Deserialize<Student>(json);
				SaveStudentData(studentData);
				string s = "{\"status\": \"success\", \"message\": \"Đã nhận thông tin sinh viên\"}";
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				response.ContentType = "application/json";
				response.ContentLength64 = bytes.Length;
				response.OutputStream.Write(bytes, 0, bytes.Length);
				response.StatusCode = 200;
				base.Dispatcher.Invoke(delegate
				{
					StopLocalServer();
					MainWindow mainWindow = new MainWindow();
					mainWindow.Show();
					Close();
				});
			}
			catch (Exception ex)
			{
				string s2 = "{\"status\": \"error\", \"message\": \"" + ex.Message + "\"}";
				byte[] bytes2 = Encoding.UTF8.GetBytes(s2);
				response.ContentType = "application/json";
				response.ContentLength64 = bytes2.Length;
				response.OutputStream.Write(bytes2, 0, bytes2.Length);
				response.StatusCode = 400;
			}
		}
		response.Close();
	}

	private void StopLocalServer()
	{
		try
		{
			if (listener != null && isServerRunning)
			{
				isServerRunning = false;
				listener.Stop();
				listener.Close();
				listener = null;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Lỗi khi dừng server: " + ex.Message);
		}
	}

	private void SaveStudentData(Student studentData)
	{
		try
		{
			if (studentData == null)
			{
				throw new ArgumentNullException("studentData", "Dữ liệu sinh viên không được null");
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			string text = Path.Combine(folderPath, "RikkeiEducationService");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			string path = Path.Combine(text, "student.json");
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			string contents = JsonSerializer.Serialize(studentData, options);
			File.WriteAllText(path, contents, Encoding.UTF8);
			Console.WriteLine("Đã lưu thông tin sinh viên: " + studentData.FullName + " - " + studentData.StudentCode);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi lưu dữ liệu: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
			throw;
		}
	}

	private void ConnectPortalButton_Click(object sender, RoutedEventArgs e)
	{
		StartLoading();
		StartLocalServer();
		if (isServerRunning)
		{
			string value = "https://portal.rikkei.edu.vn/rikkei-education-auth";
			Process.Start(new ProcessStartInfo
			{
				FileName = $"{value}?port={port}",
				UseShellExecute = true
			});
		}
	}

	private void StartLoading()
	{
		ConnectPortalButton.IsEnabled = false;
		ConnectPortalButton.Content = "Đang xác thực...";
		LoadingProgressBar.Visibility = Visibility.Visible;
	}
}
