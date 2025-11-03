using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Tesseract;

namespace rikkei_education_service;

public class AiDetect
{
	private List<string> keywords;

	private readonly Dictionary<string, List<string>> keywordVariants;

	private readonly string baseApiEndpoint;

	private readonly HttpClient httpClient;

	private MainWindow mainWindow;

	private TesseractEngine tesseractEngine;

	private bool isInitialized;

	private bool isMonitoring;

	private int monitoringInterval;

	private Task monitoringTask;

	private string violateHost;

	private int? studentId;

	private int? examId;

	private int? examRoomId;

	private readonly Dictionary<string, DateTime> lastNotificationTime;

	private const int NOTIFICATION_COOLDOWN_SECONDS = 120;

	private readonly Dictionary<string, List<DateTime>> violationCounts;

	private const int VIOLATION_THRESHOLD = 5;

	private const int VIOLATION_TIME_WINDOW_SECONDS = 60;

	private readonly Dictionary<string, DateTime> keywordFirstDetected;

	private const int KEYWORD_OBSERVATION_SECONDS = 5;

	private bool isRecording;

	private Process ffmpegProcess;

	private string videoOutputPath;

	private Task recordingLoopTask;

	private readonly string documentsPath;

	private const int VIDEO_SEGMENT_DURATION = 60;

	public AiDetect()
	{
		keywords = new List<string>();
		keywordVariants = new Dictionary<string, List<string>>();
		baseApiEndpoint = "";
		httpClient = null;
		violateHost = "";
		isInitialized = false;
		isMonitoring = false;
		monitoringInterval = 2000;
		studentId = null;
		examId = null;
		examRoomId = null;
		lastNotificationTime = new Dictionary<string, DateTime>();
		violationCounts = new Dictionary<string, List<DateTime>>();
		keywordFirstDetected = new Dictionary<string, DateTime>();
		isRecording = false;
		ffmpegProcess = null;
		videoOutputPath = "";
		documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		InitializeTesseract();
	}

	public void SetMainWindow(MainWindow mainWindow)
	{
		this.mainWindow = mainWindow;
		Task.Delay(2000).ContinueWith((Task _) => LoadKeywordsFromApiAsync());
	}

	private void InitializeTesseract()
	{
		try
		{
			tesseractEngine = null;
			isInitialized = true;
			Console.WriteLine("");
		}
		catch (Exception)
		{
			isInitialized = false;
		}
	}

	private async Task LoadKeywordsFromApiAsync()
	{
		try
		{
			string apiEndpoint = string.Concat(str2: mainWindow?.roomType?.ToLower() ?? "exam", str0: baseApiEndpoint, str1: "?roomType=");
			HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);
			if (response.IsSuccessStatusCode)
			{
				AiDetectApiResponse apiResponse = JsonSerializer.Deserialize<AiDetectApiResponse>(await response.Content.ReadAsStringAsync());
				if (apiResponse?.data != null)
				{
					keywords.Clear();
					keywordVariants.Clear();
					foreach (AiDetectData item in apiResponse.data)
					{
						if (item.status && !string.IsNullOrEmpty(item.keyword))
						{
							keywords.Add(item.keyword);
							if (!string.IsNullOrEmpty(item.serverIp) && !item.serverIp.Equals(item.keyword, StringComparison.OrdinalIgnoreCase))
							{
								keywords.Add(item.serverIp);
							}
						}
					}
					keywords = keywords.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
					foreach (string canonical in keywords)
					{
						keywordVariants[canonical] = BuildVariantsForOcr(canonical);
					}
				}
				else
				{
					LoadFallbackKeywords();
				}
			}
			else
			{
				LoadFallbackKeywords();
			}
		}
		catch (Exception)
		{
			LoadFallbackKeywords();
		}
	}

	private void LoadFallbackKeywords()
	{
		keywords.Clear();
		keywordVariants.Clear();
		Console.WriteLine("no");
	}

	private List<string> BuildVariantsForOcr(string canonical)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(canonical))
		{
			return new List<string>();
		}
		string text = canonical.Trim();
		hashSet.Add(text);
		string text2 = Regex.Replace(text, "^https?://", "", RegexOptions.IgnoreCase);
		hashSet.Add(text2);
		string text3 = text2.TrimEnd('/');
		hashSet.Add(text3);
		string text4 = text3;
		int num = text3.IndexOf('/');
		if (num >= 0)
		{
			text4 = text3.Substring(0, num);
			if (!string.IsNullOrWhiteSpace(text4))
			{
				hashSet.Add(text4);
			}
		}
		string text5 = Regex.Replace(text4, "^www\\.", "", RegexOptions.IgnoreCase);
		hashSet.Add(text5);
		if (!string.IsNullOrEmpty(text5))
		{
			hashSet.Add(text5.Replace(".", " "));
			hashSet.Add(text5.Replace(".", ""));
		}
		return hashSet.Where((string v) => !string.IsNullOrWhiteSpace(v)).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public List<string> AnalyzeScreenForAI(Bitmap bitmap)
	{
		List<string> list = new List<string>();
		if (!isInitialized || bitmap == null)
		{
			return list;
		}
		try
		{
			Pix val = ConvertBitmapToPix(bitmap);
			try
			{
				Page val2 = tesseractEngine.Process(val, (PageSegMode?)null);
				try
				{
					string text = val2.GetText();
					if (!string.IsNullOrWhiteSpace(text))
					{
						list = DetectAIKeywords(text);
						if (list.Count > 0)
						{
							LogViolation(list, text);
						}
					}
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private List<string> DetectAIKeywords(string text)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		string text2 = text.ToLower();
		string text3 = Regex.Replace(text2, "[^a-z0-9]+", " ");
		DateTime now = DateTime.Now;
		foreach (KeyValuePair<string, List<string>> keywordVariant in keywordVariants)
		{
			string key = keywordVariant.Key;
			if (!key.Contains("."))
			{
				continue;
			}
			bool flag = false;
			foreach (string item in keywordVariant.Value)
			{
				string text4 = item.ToLower();
				string value = text4.Replace(".", "");
				string value2 = text4.Replace(".", " ");
				if (text2.Contains(text4) || text2.Contains(value2) || text2.Contains(value) || text3.Contains(text4) || text3.Contains(value2) || text3.Contains(value))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				list.Add(key);
				list2.Add(key);
			}
		}
		CleanupUndetectedKeywords(list, now);
		return list2;
	}

	private void CleanupUndetectedKeywords(List<string> currentlyDetectedKeywords, DateTime now)
	{
		try
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, DateTime> item in keywordFirstDetected.ToList())
			{
				string key = item.Key;
				if (!currentlyDetectedKeywords.Contains(key))
				{
					list.Add(key);
				}
			}
			foreach (string item2 in list)
			{
				keywordFirstDetected.Remove(item2);
			}
		}
		catch (Exception)
		{
		}
	}

	private void LogViolation(List<string> detectedKeywords, string contextText)
	{
		string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		string text2 = string.Join(", ", detectedKeywords);
		ShowViolationNotificationWithCooldown(detectedKeywords);
		//UploadViolationToServer(detectedKeywords, contextText);
	}

	private Pix ConvertBitmapToPix(Bitmap bitmap)
	{
		try
		{
			using MemoryStream memoryStream = new MemoryStream();
			//bitmap.Save(memoryStream, ImageFormat.Png);
			byte[] array = memoryStream.ToArray();
			return Pix.LoadFromMemory(array);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public List<string> AnalyzeCurrentScreen()
	{
		try
		{
			Rectangle bounds = Screen.PrimaryScreen.Bounds;
			using Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
			using Graphics graphics = Graphics.FromImage(bitmap);
			graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
			return AnalyzeScreenForAI(bitmap);
		}
		catch (Exception)
		{
			return new List<string>();
		}
	}

	public void AddKeyword(string keyword)
	{
		if (!string.IsNullOrWhiteSpace(keyword) && !keywords.Contains(keyword))
		{
			keywords.Add(keyword);
		}
	}

	public void RemoveKeyword(string keyword)
	{
		if (keywords.Contains(keyword))
		{
			keywords.Remove(keyword);
		}
	}

	public List<string> GetKeywords()
	{
		return new List<string>(keywords);
	}

	public async Task ReloadKeywordsAsync()
	{
		await LoadKeywordsFromApiAsync();
	}

	public void ClearKeywordObservation()
	{
		try
		{
			keywordFirstDetected.Clear();
		}
		catch (Exception)
		{
		}
	}

	public void ClearKeywordObservation(string keyword)
	{
		try
		{
			if (keywordFirstDetected.ContainsKey(keyword))
			{
				keywordFirstDetected.Remove(keyword);
			}
		}
		catch (Exception)
		{
		}
	}

	public void StartMonitoring()
	{
		try
		{
			if (!isInitialized)
			{
				isInitialized = true;
			}
			if (!isMonitoring)
			{
				isMonitoring = true;
				monitoringTask = Task.CompletedTask;
				Console.WriteLine("[AiDetect] Monitoring started (mock mode, no real detection).");
				isRecording = false;
			}
		}
		catch (Exception ex)
		{
			isMonitoring = false;
			Console.WriteLine("[AiDetect] Monitoring disabled due to: " + ex.Message);
		}
	}

	public void StopMonitoring()
	{
		isMonitoring = false;
		monitoringTask?.Wait(1000);
		StopVideoRecording();
		ClearKeywordObservation();
	}

	private async Task MonitoringLoop()
	{
		while (isMonitoring)
		{
			try
			{
				List<string> detectedKeywords = AnalyzeCurrentScreen();
				if (detectedKeywords.Count > 0)
				{
				}
				await Task.Delay(monitoringInterval);
			}
			catch (Exception)
			{
				await Task.Delay(monitoringInterval);
			}
		}
	}

	public bool IsMonitoring()
	{
		return isMonitoring;
	}

	public void SetMonitoringInterval(int intervalMs)
	{
		if (intervalMs > 0)
		{
			monitoringInterval = intervalMs;
		}
	}

	public int GetMonitoringInterval()
	{
		return monitoringInterval;
	}

	public void SetStudentExamInfo(int studentId, int examId, int examRoomId = 0)
	{
		this.studentId = studentId;
		this.examId = examId;
		this.examRoomId = examRoomId;
		if (isMonitoring && examRoomId > 0 && !isRecording)
		{
			StartVideoRecording();
		}
	}

	//private async Task UploadViolationToServer(List<string> detectedKeywords, string contextText)
	//{
	//	//try
	//	//{
	//	//	List<string> keywordsToUpload = new List<string>();
	//	//	foreach (string keyword in detectedKeywords)
	//	//	{
	//	//		if (ViolationTracker.CanSendReport(keyword))
	//	//		{
	//	//			keywordsToUpload.Add(keyword);
	//	//		}
	//	//	}
	//	//	if (keywordsToUpload.Count == 0)
	//	//	{
	//	//		return;
	//	//	}
	//	//	if (!studentId.HasValue || !examId.HasValue)
	//	//	{
	//	//		try
	//	//		{
	//	//			if (mainWindow != null)
	//	//			{
	//	//				if (!studentId.HasValue && mainWindow.student != null && mainWindow.student.StudentId > 0)
	//	//				{
	//	//					studentId = mainWindow.student.StudentId;
	//	//				}
	//	//				if (!examId.HasValue && mainWindow.examId > 0)
	//	//				{
	//	//					examId = mainWindow.examId;
	//	//				}
	//	//			}
	//	//		}
	//	//		catch
	//	//		{
	//	//		}
	//	//	}
	//	//	if (string.IsNullOrEmpty(violateHost) || !studentId.HasValue || !examId.HasValue)
	//	//	{
	//	//		return;
	//	//	}
	//	//	byte[] screenshotBytes = await GetScreenshotAsync();
	//	//	if (screenshotBytes == null)
	//	//	{
	//	//		return;
	//	//	}
	//	//	string keywordsString = string.Join(", ", keywordsToUpload);
	//	//	string violateDescription = keywordsString;
	//	//	await SendViolateReportToServer(violateDescription, screenshotBytes);
	//	//	foreach (string keyword2 in keywordsToUpload)
	//	//	{
	//	//		ViolationTracker.MarkReportSent(keyword2);
	//	//	}
	//	//}
	//	//catch (Exception)
	//	//{
	//	//}
	//	return;
 //   }

	//private async Task SendViolateReportToServer(string violateDescription, byte[] screenshotBytes)
	//{
	//	//try
	//	//{
	//	//	string apiEndpoint = violateHost + "/exam/init-violate";
	//	//	string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N");
	//	//	using HttpClient httpClient = new HttpClient();
	//	//	MultipartFormDataContent multipartContent = new MultipartFormDataContent(boundary);
	//	//	StringContent studentIdContent = new StringContent(studentId.Value.ToString());
	//	//	multipartContent.Add(studentIdContent, "studentId");
	//	//	StringContent examIdContent = new StringContent(examId.Value.ToString());
	//	//	multipartContent.Add(examIdContent, "examId");
	//	//	StringContent descriptionContent = new StringContent(violateDescription);
	//	//	multipartContent.Add(descriptionContent, "violateDescription");
	//	//	ByteArrayContent imageContent = new ByteArrayContent(screenshotBytes);
	//	//	imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
	//	//	multipartContent.Add(imageContent, "image", "ai_violation_screenshot.jpg");
	//	//	HttpResponseMessage response = await httpClient.PostAsync(apiEndpoint, multipartContent);
	//	//	if (!response.IsSuccessStatusCode)
	//	//	{
	//	//		await response.Content.ReadAsStringAsync();
	//	//	}
	//	//}
	//	//catch (Exception)
	//	//{
	//	//	throw;
	//	//}
	//	return;
 //   }

	private async Task<byte[]> GetScreenshotAsync()
	{
		try
		{
			using Bitmap bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size);
			}
			using MemoryStream memoryStream = new MemoryStream();
			ImageCodecInfo jpegEncoder = GetJpegEncoder();
			EncoderParameters encoderParams = new EncoderParameters(1);
			encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
			bitmap.Save(memoryStream, jpegEncoder, encoderParams);
			return memoryStream.ToArray();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private ImageCodecInfo GetJpegEncoder()
	{
		ImageCodecInfo[] imageDecoders = ImageCodecInfo.GetImageDecoders();
		ImageCodecInfo[] array = imageDecoders;
		foreach (ImageCodecInfo imageCodecInfo in array)
		{
			//if (imageCodecInfo.FormatID == ImageFormat.Jpeg.Guid)
			{
				return imageCodecInfo;
			}
		}
		return null;
	}

	private void ShowViolationNotificationWithCooldown(List<string> detectedKeywords)
	{
		try
		{
			string text = string.Join(", ", detectedKeywords);
			bool flag = false;
			List<string> list = new List<string>();
			foreach (string detectedKeyword in detectedKeywords)
			{
				RecordViolation(detectedKeyword);
				if (ShouldShowNotificationForKeyword(detectedKeyword))
				{
					flag = true;
					list.Add(detectedKeyword);
					UpdateLastNotificationTime(detectedKeyword);
				}
			}
			if (flag)
			{
				string text2 = $"\ud83d\udea8 CẢNH BÁO VI PHẠM AI / LÀM VIỆC RIÊNG!\n\nHệ thống đã phát hiện bạn đang sử dụng các công cụ AI:\n• {string.Join(", ", list)}\n\nVui lòng tập trung vào làm bài thi / học tập!";
				ShowTrayNotification(list);
			}
		}
		catch (Exception)
		{
		}
	}

	private bool IsNotificationAllowed(string keyword)
	{
		if (!lastNotificationTime.ContainsKey(keyword))
		{
			return true;
		}
		DateTime dateTime = lastNotificationTime[keyword];
		return (DateTime.Now - dateTime).TotalSeconds >= 120.0;
	}

	private void UpdateLastNotificationTime(string keyword)
	{
		lastNotificationTime[keyword] = DateTime.Now;
	}

	private void RecordViolation(string keyword)
	{
		if (!violationCounts.ContainsKey(keyword))
		{
			violationCounts[keyword] = new List<DateTime>();
		}
		violationCounts[keyword].Add(DateTime.Now);
		DateTime cutoffTime = DateTime.Now.AddSeconds(-60.0);
		violationCounts[keyword] = violationCounts[keyword].Where((DateTime time) => time > cutoffTime).ToList();
	}

	private bool ShouldShowNotificationForKeyword(string keyword)
	{
		if (!IsNotificationAllowed(keyword))
		{
			return false;
		}
		if (!violationCounts.ContainsKey(keyword))
		{
			return false;
		}
		return violationCounts[keyword].Count >= 5;
	}

	private void ShowTrayNotification(List<string> keywords)
	{
		try
		{
			string text = string.Join(", ", keywords);
			string balloonTipTitle = "\ud83d\udea8 Cảnh Báo Vi Phạm AI";
			string balloonTipText = "Phát hiện: " + text + "\nVui lòng tập trung làm bài!";
			using NotifyIcon notifyIcon = new NotifyIcon();
			string text2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "icon", "icon.ico");
			if (File.Exists(text2))
			{
				notifyIcon.Icon = new Icon(text2);
			}
			else
			{
				notifyIcon.Icon = SystemIcons.Warning;
			}
			notifyIcon.Visible = true;
			notifyIcon.BalloonTipTitle = balloonTipTitle;
			notifyIcon.BalloonTipText = balloonTipText;
			notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
			notifyIcon.ShowBalloonTip(5000);
			notifyIcon.Visible = false;
		}
		catch (Exception)
		{
			System.Windows.MessageBox.Show("\ud83d\udea8 CẢNH BÁO VI PHẠM AI!\n\nPhát hiện: " + string.Join(", ", keywords) + "\nVui lòng tập trung làm bài!", "Cảnh Báo Vi Phạm AI", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void InitializeVideoRecording()
	{
	}

	private void StartVideoRecording()
	{
		try
		{
			if (!isRecording)
			{
				isRecording = true;
				Console.WriteLine("[AiDetect] Video recording is disabled (mock mode).");
				recordingLoopTask = Task.CompletedTask;
			}
		}
		catch (Exception)
		{
			isRecording = false;
		}
	}

	private void StopVideoRecording()
	{
		try
		{
			if (!isRecording && ffmpegProcess == null)
			{
				return;
			}
			isRecording = false;
			if (ffmpegProcess != null && !ffmpegProcess.HasExited)
			{
				try
				{
					ffmpegProcess.StandardInput.Write("q\n");
					ffmpegProcess.StandardInput.Close();
					if (!ffmpegProcess.WaitForExit(7000))
					{
						try
						{
							ffmpegProcess.Kill();
							ffmpegProcess.WaitForExit(1000);
						}
						catch (Exception)
						{
						}
					}
				}
				catch (Exception)
				{
					try
					{
						ffmpegProcess.Kill();
					}
					catch (Exception)
					{
					}
				}
			}
			try
			{
				recordingLoopTask?.Wait(3000);
			}
			catch
			{
			}
			try
			{
				ffmpegProcess?.Dispose();
			}
			catch (Exception)
			{
			}
			finally
			{
				ffmpegProcess = null;
			}
		}
		catch (Exception)
		{
		}
	}

	private void CleanupOldDateFolders()
	{
		try
		{
			string path = Path.Combine(documentsPath, "RikkeiEducation", "ScreenRecordings");
			if (!Directory.Exists(path))
			{
				return;
			}
			DateTime today = DateTime.Today;
			string[] directories = Directory.GetDirectories(path);
			string[] array = directories;
			foreach (string path2 in array)
			{
				try
				{
					string fileName = Path.GetFileName(path2);
					if (string.IsNullOrWhiteSpace(fileName))
					{
						continue;
					}
					int num = fileName.IndexOf('_');
					if (num > 0)
					{
						string s = fileName.Substring(0, num);
						if (DateTime.TryParseExact(s, "yyyy-MM-dd", null, DateTimeStyles.None, out var result) && result < today)
						{
							Directory.Delete(path2, recursive: true);
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private void CleanupOldVideos()
	{
		try
		{
			string text = DateTime.Now.ToString("yyyy-MM-dd");
			string text2 = examRoomId?.ToString() ?? "unknown";
			string path = Path.Combine(documentsPath, "RikkeiEducation", "ScreenRecordings", text + "_Room" + text2);
			if (!Directory.Exists(path))
			{
				return;
			}
			string[] files = Directory.GetFiles(path, "screen_recording_*.wmv");
			string[] files2 = Directory.GetFiles(path, "screen_recording_*.mp4");
			List<FileInfo> list = (from f in files.Concat(files2)
				select new FileInfo(f) into f
				orderby f.CreationTime descending
				select f).ToList();
			if (list.Count <= 10)
			{
				return;
			}
			IEnumerable<FileInfo> enumerable = list.Skip(10);
			foreach (FileInfo item in enumerable)
			{
				try
				{
					item.Delete();
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public string GetCurrentVideoPath()
	{
		return videoOutputPath;
	}

	public bool IsRecording()
	{
		return isRecording;
	}

	public void StopVideoRecordingPublic()
	{
		StopVideoRecording();
	}

	public void StartVideoRecordingPublic()
	{
		StartVideoRecording();
	}

	public void ForceStopVideoRecording()
	{
		try
		{
			isRecording = false;
			if (ffmpegProcess != null && !ffmpegProcess.HasExited)
			{
				try
				{
					ffmpegProcess.Kill();
					ffmpegProcess.WaitForExit(1000);
				}
				catch (Exception)
				{
				}
			}
			ffmpegProcess?.Dispose();
			ffmpegProcess = null;
		}
		catch (Exception)
		{
		}
	}

	public void Dispose()
	{
		try
		{
			isMonitoring = false;
			monitoringTask?.Wait(2000);
			ForceStopVideoRecording();
			ClearKeywordObservation();
			TesseractEngine obj = tesseractEngine;
			if (obj != null)
			{
				((DisposableBase)obj).Dispose();
			}
			tesseractEngine = null;
			httpClient?.Dispose();
			isInitialized = false;
		}
		catch (Exception)
		{
		}
	}
}
