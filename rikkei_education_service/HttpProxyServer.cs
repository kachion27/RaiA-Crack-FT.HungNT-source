using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rikkei_education_service;

public class HttpProxyServer
{
	private MainWindow mainWindow;

	private TcpListener listener;

	public bool isRunning = false;

	private int port = 8888;

	private string violateHost = Environment.GetEnvironmentVariable("VIOLATE_HOST");

	public bool blockUrl = false;

	private readonly Dictionary<string, DateTime> lastNotificationTime = new Dictionary<string, DateTime>();

	private const int NOTIFICATION_COOLDOWN_SECONDS = 120;

	private readonly string baseApiEndpoint = "https://api.raia.edu.vn/api/rikkei-education-app/find-ai-detect-data";

	private readonly HttpClient httpClient = new HttpClient();

	private List<string> aiKeyWords = new List<string>();

	public HttpProxyServer(MainWindow mainWindow)
	{
		this.mainWindow = mainWindow;
		Task.Delay(2000).ContinueWith((Task _) => LoadKeywordsFromApiAsync());
	}

	private async Task LoadKeywordsFromApiAsync()
	{
		try
		{
			string apiEndpoint = string.Concat(str2: mainWindow.roomType?.ToLower() ?? "exam", str0: baseApiEndpoint, str1: "?roomType=");
			HttpResponseMessage response = await httpClient.GetAsync(apiEndpoint);
			if (response.IsSuccessStatusCode)
			{
				AiDetectApiResponse apiResponse = JsonSerializer.Deserialize<AiDetectApiResponse>(await response.Content.ReadAsStringAsync());
				if (apiResponse?.data != null)
				{
					aiKeyWords.Clear();
					foreach (AiDetectData item in apiResponse.data)
					{
						if (item.status && !string.IsNullOrEmpty(item.keyword))
						{
							aiKeyWords.Add(item.keyword);
							if (!string.IsNullOrEmpty(item.serverIp) && !item.serverIp.Equals(item.keyword, StringComparison.OrdinalIgnoreCase))
							{
								aiKeyWords.Add(item.serverIp);
							}
						}
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
		aiKeyWords = new List<string>
		{
			"openai", "chat.com", "bard", "claude", "perplexity", "poe", "copilot", "quillbot", "grammarly", "jasper",
			"lm", "gpt", "cursor", "gemini", "chatgpt", "chat.openai.com", "huggingface", "llama", "llm", "mistral",
			"groq", "moonshot", "notion ai", "you.com", "writesonic", "phind", "tabnine", "codewhisperer", "replika", "deepai",
			"character.ai", "codeium", "deepmind", "tome.app", "shortlyai", "copy.ai", "inferkit", "type.ai", "chatpdf", "upword",
			"ai21", "cohere", "wordtune", "scribe", "simplified", "writer.com", "peppertype", "frase", "anyword", "hypotenuse",
			"scalenut", "surferseo", "textcortex", "lex.page", "writecream", "rytr", "neuroflash", "lightpdf", "voicemaker", "suno",
			"heygen", "liên minh huyền thoại", "dota", "facebook", "zalo", "tiktok", "youtube", "riot"
		};
	}

	public void Start()
	{
		try
		{
			isRunning = true;
			listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			AcceptConnectionsAsync();
		}
		catch (Exception)
		{
			throw;
		}
	}

	public void Stop()
	{
		try
		{
			isRunning = false;
			listener?.Stop();
		}
		catch (Exception)
		{
		}
	}

	private async Task AcceptConnectionsAsync()
	{
		while (isRunning)
		{
			try
			{
				TcpClient client = await listener.AcceptTcpClientAsync();
				_ = (IPEndPoint)client.Client.RemoteEndPoint;
				HandleClientAsync(client);
			}
			catch (Exception)
			{
				if (!isRunning)
				{
				}
			}
		}
	}

	private async Task HandleClientAsync(TcpClient client)
	{
		try
		{
			using (client)
			{
				using NetworkStream stream = client.GetStream();
				HttpRequest request = await ReadHttpRequestAsync(stream);
				if (request == null)
				{
					return;
				}
				var (shouldBlock, blockedKeyword) = await CheckViolate($"{request.Method} {request.Url} {request.Host}");
				if (shouldBlock)
				{
					await SendBlockedResponseAsync(stream, blockedKeyword);
					return;
				}
				if (IsAIService(request.Url))
				{
				}
				if (!(request.Method.ToUpper() == "CONNECT"))
				{
					await SendResponseAsync(stream, await ForwardRequestAsync(request));
					return;
				}
				(bool shouldBlock, string blockedKeyword) tuple2 = await CheckViolate("CONNECT " + request.Url);
				var (shouldBlockConnect, _) = tuple2;
				_ = tuple2.blockedKeyword;
				if (shouldBlockConnect)
				{
					string errorResponse = "HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n";
					byte[] responseBytes = Encoding.ASCII.GetBytes(errorResponse);
					await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
					return;
				}
				await HandleConnectMethodAsync(stream, request);
			}
		}
		catch (Exception)
		{
		}
	}

	private async Task HandleConnectMethodAsync(NetworkStream clientStream, HttpRequest request)
	{
		try
		{
			string[] hostParts = request.Url.Split(':');
			string host = hostParts[0];
			int port = ((hostParts.Length > 1) ? int.Parse(hostParts[1]) : 443);
			try
			{
				using TcpClient targetClient = new TcpClient();
				await targetClient.ConnectAsync(host, port);
				string successResponse = "HTTP/1.1 200 Connection Established\r\n\r\n";
				byte[] responseBytes = Encoding.ASCII.GetBytes(successResponse);
				await clientStream.WriteAsync(responseBytes, 0, responseBytes.Length);
				using NetworkStream targetStream = targetClient.GetStream();
				Task task1 = TunnelDataAsync(clientStream, targetStream, "Client → Target");
				Task task2 = TunnelDataAsync(targetStream, clientStream, "Target → Client");
				await Task.WhenAll(task1, task2);
			}
			catch (Exception)
			{
				string errorResponse = "HTTP/1.1 502 Bad Gateway\r\n\r\n";
				byte[] responseBytes2 = Encoding.ASCII.GetBytes(errorResponse);
				await clientStream.WriteAsync(responseBytes2, 0, responseBytes2.Length);
			}
		}
		catch (Exception)
		{
		}
	}

	private async Task TunnelDataAsync(NetworkStream from, NetworkStream to, string direction)
	{
		try
		{
			byte[] buffer = new byte[4096];
			while (true)
			{
				int num;
				int bytesRead = (num = await from.ReadAsync(buffer, 0, buffer.Length));
				if (num <= 0)
				{
					break;
				}
				await to.WriteAsync(buffer, 0, bytesRead);
			}
		}
		catch (Exception)
		{
		}
	}

	private async Task<HttpRequest> ReadHttpRequestAsync(NetworkStream stream)
	{
		try
		{
			byte[] buffer = new byte[4096];
			StringBuilder requestBuilder = new StringBuilder();
			string chunk;
			do
			{
				int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
				if (bytesRead == 0)
				{
					break;
				}
				chunk = Encoding.ASCII.GetString(buffer, 0, bytesRead);
				requestBuilder.Append(chunk);
			}
			while (!chunk.Contains("\r\n\r\n"));
			string requestText = requestBuilder.ToString();
			return ParseHttpRequest(requestText);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private HttpRequest ParseHttpRequest(string requestText)
	{
		try
		{
			string[] array = requestText.Split('\n');
			string text = array[0];
			string[] array2 = text.Split(' ');
			if (array2.Length >= 3)
			{
				string method = array2[0];
				string text2 = array2[1];
				string text3 = ExtractHostFromHeaders(array);
				return new HttpRequest
				{
					Method = method,
					Url = text2,
					Host = text3,
					FullUrl = text3 + text2,
					Headers = array
				};
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private string ExtractHostFromHeaders(string[] headers)
	{
		try
		{
			foreach (string text in headers)
			{
				if (text.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
				{
					return text.Substring(5).Trim();
				}
			}
		}
		catch (Exception)
		{
		}
		return "localhost";
	}

	private async Task<(bool shouldBlock, string blockedKeyword)> CheckViolate(string rawRequest)
	{
		foreach (string aiKeyword in aiKeyWords)
		{
			if (!rawRequest.Contains(aiKeyword))
			{
				continue;
			}
			ShowViolationNotificationWithCooldown(aiKeyword, rawRequest);
			if (ViolationTracker.CanSendReport(aiKeyword))
			{
				Student student = mainWindow.student;
				if (student != null && student.StudentId > 0 && mainWindow.examId > 0)
				{
					try
					{
						await SendViolateReport(rawRequest, aiKeyword);
						ViolationTracker.MarkReportSent(aiKeyword);
					}
					catch (Exception)
					{
					}
				}
			}
			if (blockUrl)
			{
				return (shouldBlock: true, blockedKeyword: aiKeyword);
			}
			break;
		}
		return (shouldBlock: false, blockedKeyword: null);
	}

	private async Task SendViolateReport(string rawRequest, string detectedKeyword)
	{
		try
		{
			string apiEndpoint = violateHost + "/exam/init-violate";
			int studentId = mainWindow.student.StudentId;
			int examId = mainWindow.examId;
			byte[] screenshotBytes = await GetScreenshotAsync();
			if (screenshotBytes != null)
			{
				string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N");
				using HttpClient httpClient = new HttpClient();
				MultipartFormDataContent multipartContent = new MultipartFormDataContent(boundary);
				StringContent studentIdContent = new StringContent(studentId.ToString());
				multipartContent.Add(studentIdContent, "studentId");
				StringContent examIdContent = new StringContent(examId.ToString());
				multipartContent.Add(examIdContent, "examId");
				StringContent descriptionContent = new StringContent(detectedKeyword);
				multipartContent.Add(descriptionContent, "violateDescription");
				ByteArrayContent imageContent = new ByteArrayContent(screenshotBytes);
				imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
				multipartContent.Add(imageContent, "image", "screenshot.jpg");
				await httpClient.PostAsync(apiEndpoint, multipartContent);
			}
		}
		catch (Exception)
		{
			throw;
		}
	}

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
			encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
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
			if (imageCodecInfo.FormatID == ImageFormat.Jpeg.Guid)
			{
				return imageCodecInfo;
			}
		}
		return null;
	}

	public async Task ReloadKeywordsAsync()
	{
		await LoadKeywordsFromApiAsync();
	}

	public List<string> GetKeywords()
	{
		return new List<string>(aiKeyWords);
	}

	private void ShowViolationNotificationWithCooldown(string detectedKeyword, string rawRequest)
	{
		try
		{
			if (IsNotificationAllowed(detectedKeyword))
			{
				string text = $"\ud83d\udea8 CẢNH BÁO VI PHẠM AI / LÀM VIỆC RIÊNG!\n\nHệ thống đã phát hiện bạn đang truy cập:\n• {detectedKeyword}\n\nĐây là một công cụ AI không được phép sử dụng trong kỳ thi.\nVui lòng tập trung vào làm bài thi / học tập!";
				UpdateLastNotificationTime(detectedKeyword);
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

	private bool IsAIService(string url)
	{
		try
		{
			string text = url;
			int num = url.IndexOf(':');
			if (num > 0)
			{
				text = url.Substring(0, num);
			}
			string text2 = text.ToLower();
			foreach (string aiKeyWord in aiKeyWords)
			{
				if (text2.Contains(aiKeyWord.ToLower()))
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	private async Task<HttpResponse> ForwardRequestAsync(HttpRequest request)
	{
		try
		{
			HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(request.FullUrl);
			httpRequest.Method = request.Method;
			string[] headers = request.Headers;
			foreach (string header in headers)
			{
				if (header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase) || header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				int colonIndex = header.IndexOf(':');
				if (colonIndex <= 0)
				{
					continue;
				}
				string name = header.Substring(0, colonIndex).Trim();
				string value = header.Substring(colonIndex + 1).Trim();
				if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
				{
					try
					{
						httpRequest.Headers.Add(name, value);
					}
					catch
					{
					}
				}
			}
			using HttpWebResponse response = (HttpWebResponse)(await httpRequest.GetResponseAsync());
			using Stream responseStream = response.GetResponseStream();
			StringBuilder responseBuilder = new StringBuilder();
			StringBuilder stringBuilder = responseBuilder;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(7, 3, stringBuilder);
			handler.AppendLiteral("HTTP/");
			handler.AppendFormatted(response.ProtocolVersion);
			handler.AppendLiteral(" ");
			handler.AppendFormatted((int)response.StatusCode);
			handler.AppendLiteral(" ");
			handler.AppendFormatted(response.StatusDescription);
			stringBuilder2.AppendLine(ref handler);
			string[] allKeys = response.Headers.AllKeys;
			foreach (string headerName in allKeys)
			{
				string headerValue = response.Headers[headerName];
				stringBuilder = responseBuilder;
				StringBuilder stringBuilder3 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder);
				handler.AppendFormatted(headerName);
				handler.AppendLiteral(": ");
				handler.AppendFormatted(headerValue);
				stringBuilder3.AppendLine(ref handler);
			}
			responseBuilder.AppendLine();
			if (responseStream != null)
			{
				byte[] buffer = new byte[4096];
				while (true)
				{
					int num;
					int bytesRead = (num = await responseStream.ReadAsync(buffer, 0, buffer.Length));
					if (num <= 0)
					{
						break;
					}
					responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
				}
			}
			return new HttpResponse
			{
				StatusCode = (int)response.StatusCode,
				Headers = response.Headers,
				Body = responseBuilder.ToString()
			};
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			return new HttpResponse
			{
				StatusCode = 500,
				Body = "Error: " + ex2.Message
			};
		}
	}

	private async Task SendBlockedResponseAsync(NetworkStream stream, string blockedKeyword)
	{
		try
		{
			string blockedHtml = "\r\n<!DOCTYPE html>\r\n<html>\r\n<head>\r\n    <meta charset='UTF-8'>\r\n    <title>Truy cập bị chặn</title>\r\n    <style>\r\n        body { \r\n            font-family: Arial, sans-serif; \r\n            text-align: center; \r\n            background-color: #f8f9fa;\r\n            margin: 0;\r\n            padding: 50px;\r\n        }\r\n        .warning-box {\r\n            background-color: #fff3cd;\r\n            border: 2px solid #ffc107;\r\n            border-radius: 10px;\r\n            padding: 30px;\r\n            max-width: 600px;\r\n            margin: 0 auto;\r\n            box-shadow: 0 4px 6px rgba(0,0,0,0.1);\r\n        }\r\n        .warning-icon {\r\n            font-size: 48px;\r\n            color: #dc3545;\r\n            margin-bottom: 20px;\r\n        }\r\n        h1 {\r\n            color: #dc3545;\r\n            margin-bottom: 20px;\r\n        }\r\n        .keyword {\r\n            background-color: #f8d7da;\r\n            color: #721c24;\r\n            padding: 5px 10px;\r\n            border-radius: 5px;\r\n            font-weight: bold;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <div class='warning-box'>\r\n        <div class='warning-icon'>\ud83d\udeab</div>\r\n        <h1>TRUY CẬP BỊ CHẶN</h1>\r\n        <p>Hệ thống đã phát hiện bạn đang cố gắng truy cập:</p>\r\n        <p class='keyword'>" + blockedKeyword + "</p>\r\n        <p><strong>Đây là một trang web/dịch vụ không được phép sử dụng trong kỳ thi.</strong></p>\r\n        <p>Vui lòng tập trung vào làm bài thi và không truy cập các trang web không liên quan!</p>\r\n        <hr>\r\n        <p><em>Rikkei Education Service - Hệ thống giám sát thi trực tuyến</em></p>\r\n    </div>\r\n</body>\r\n</html>";
			string responseText = "HTTP/1.1 403 Forbidden\r\n";
			responseText += "Content-Type: text/html; charset=UTF-8\r\n";
			responseText += $"Content-Length: {Encoding.UTF8.GetByteCount(blockedHtml)}\r\n";
			responseText += "Connection: close\r\n";
			responseText += "\r\n";
			responseText += blockedHtml;
			byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
			await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
		}
		catch (Exception)
		{
		}
	}

	private async Task SendResponseAsync(NetworkStream stream, HttpResponse response)
	{
		try
		{
			string responseText = $"HTTP/1.1 {response.StatusCode} OK\r\n";
			responseText += "Content-Type: text/html\r\n";
			responseText += $"Content-Length: {response.Body?.Length ?? 0}\r\n";
			responseText += "\r\n";
			responseText += response.Body;
			byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
			await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
		}
		catch (Exception)
		{
		}
	}
}
