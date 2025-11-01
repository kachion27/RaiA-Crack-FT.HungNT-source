#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace rikkei_education_service;

internal class StudentSocket
{
	private MainWindow _mainWindow;

	private MainSocket _mainSocket;

	private ScreenCaptureService screenCaptureService;

	public StudentSocket(MainWindow mainWindow, MainSocket mainSocket)
	{
		StudentSocket studentSocket = this;
		_mainWindow = mainWindow;
		_mainSocket = mainSocket;
		_mainSocket.OnMessageReceived("auth_response", delegate
		{
		});
		_mainSocket.OnMessageReceived("open_link", delegate(object link)
		{
			try
			{
				Debug.WriteLine($"Nhận được link: {link}");
				string text = link.ToString();
				if (text.StartsWith("[") && text.EndsWith("]"))
				{
					string text2 = text.Substring(1, text.Length - 2);
					if (text2.StartsWith("\"") && text2.EndsWith("\""))
					{
						text2 = text2.Substring(1, text2.Length - 2);
					}
					Process.Start(new ProcessStartInfo
					{
						FileName = text2,
						UseShellExecute = true
					});
				}
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("set_frame_rate", delegate(object data)
		{
			try
			{
				string s = data.ToString();
				if (int.TryParse(s, out var result))
				{
					studentSocket.SetFrameRate(result);
				}
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("set_quality", delegate(object data)
		{
			try
			{
				string s = data.ToString();
				if (int.TryParse(s, out var result))
				{
					studentSocket.SetQuality(result);
				}
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("start_screen_sharing", delegate
		{
			studentSocket.StartScreenSharing();
		});
		_mainSocket.OnMessageReceived("stop_screen_sharing", delegate
		{
			studentSocket.StopScreenSharing();
		});
		_mainSocket.OnMessageReceived("join_exam_room", delegate(object data)
		{
			try
			{
				if (data != null)
				{
					string text = data.ToString();
					if (text.StartsWith("[") && text.EndsWith("]"))
					{
						string text2 = text.Substring(1, text.Length - 2);
						if (text2.StartsWith("\"") && text2.EndsWith("\""))
						{
							text2 = text2.Substring(1, text2.Length - 2);
						}
						if (int.TryParse(text2, out var examId) && mainWindow.examId != examId)
						{
							mainWindow.examId = examId;
							studentSocket._mainWindow.Dispatcher.Invoke(delegate
							{
								try
								{
									if (mainWindow.proxyServer == null)
									{
										mainWindow.UpdateApplicationStatus($"Tham gia phòng thi / học: {examId} ");
										mainWindow.StartLoadAiDetect();
									}
									if (mainWindow.aiDetect == null)
									{
										mainWindow.StartLoadAiDetect();
									}
									if (mainWindow.student != null && mainWindow.aiDetect != null)
									{
										mainWindow.aiDetect.SetStudentExamInfo(mainWindow.student.StudentId, examId, examId);
										Debug.WriteLine($"✅ Updated AI detect with examRoomId: {examId}, studentId: {mainWindow.student.StudentId}");
									}
								}
								catch (Exception ex2)
								{
									Debug.WriteLine("❌ Lỗi khởi động proxy server: " + ex2.Message);
									mainWindow.UpdateApplicationStatus($"Tham gia phòng thi: {examId} - Lỗi proxy: {ex2.Message}");
								}
							});
						}
					}
				}
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("leave_exam_room", delegate
		{
			try
			{
				studentSocket._mainWindow.examId = 0;
				if (mainWindow.aiDetect != null)
				{
					mainWindow.aiDetect.SetStudentExamInfo(mainWindow.student.StudentId, 0);
				}
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					mainWindow.ShowBalloonTip("Rikkei Education Service", "Hết giờ làm bài, tắt proxy server!");
					mainWindow.RestoreProxy();
					mainWindow.UpdateApplicationStatus("Đang đợi lệnh");
					mainWindow.aiDetect.StopMonitoring();
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("set_room_type", delegate(object data)
		{
			try
			{
				if (data != null)
				{
					string dataString = data.ToString();
					if (dataString.StartsWith("[") && dataString.EndsWith("]"))
					{
						string roomTypeString = dataString.Substring(1, dataString.Length - 2);
						if (roomTypeString.StartsWith("\"") && roomTypeString.EndsWith("\""))
						{
							roomTypeString = roomTypeString.Substring(1, roomTypeString.Length - 2);
						}
						if ((roomTypeString == "EXAM" || roomTypeString == "CLASS") && !(studentSocket._mainWindow.roomType == roomTypeString))
						{
							studentSocket._mainWindow.roomType = roomTypeString;
							studentSocket._mainWindow.Dispatcher.Invoke(delegate
							{
								studentSocket._mainWindow.UpdateApplicationStatus("Room Type: " + roomTypeString);
							});
						}
					}
					else if ((dataString == "EXAM" || dataString == "CLASS") && !(studentSocket._mainWindow.roomType == dataString))
					{
						studentSocket._mainWindow.roomType = dataString;
						studentSocket._mainWindow.Dispatcher.Invoke(delegate
						{
							studentSocket._mainWindow.UpdateApplicationStatus("Room Type: " + dataString);
						});
					}
				}
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("lock_screen", delegate
		{
			try
			{
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					studentSocket._mainWindow.LockScreen();
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("unlock_screen", delegate
		{
			try
			{
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					studentSocket._mainWindow.UnlockScreen();
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("open_folder_video", delegate
		{
			try
			{
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					if (mainWindow.aiDetect != null)
					{
						mainWindow.aiDetect.StopVideoRecordingPublic();
						Task.Delay(1000).ContinueWith(delegate
						{
							mainWindow.aiDetect.StartVideoRecordingPublic();
						});
					}
					studentSocket.OpenVideoFolder();
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("clean_video_folder", delegate
		{
			try
			{
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					studentSocket.CleanVideoFolder();
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("block_url", delegate
		{
			try
			{
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					if (mainWindow.proxyServer != null)
					{
						mainWindow.proxyServer.blockUrl = true;
						mainWindow.UpdateApplicationStatus("Đã bật chế độ chặn URL vi phạm");
					}
				});
			}
			catch (Exception)
			{
			}
		});
		_mainSocket.OnMessageReceived("inbound_msg", delegate(object data)
		{
			try
			{
				string message;
				string text = (message = ((data == null) ? string.Empty : data.ToString()));
				try
				{
					if (!string.IsNullOrWhiteSpace(text))
					{
						if (text.TrimStart().StartsWith("["))
						{
							string[] array = JsonSerializer.Deserialize<string[]>(text);
							if (array != null && array.Length != 0)
							{
								message = array[0];
							}
						}
						else if (text.TrimStart().StartsWith("\""))
						{
							string text2 = JsonSerializer.Deserialize<string>(text);
							if (text2 != null)
							{
								message = text2;
							}
						}
					}
				}
				catch
				{
				}
				studentSocket._mainWindow.Dispatcher.Invoke(delegate
				{
					try
					{
						studentSocket._mainWindow.Activate();
					}
					catch
					{
					}
					MessageBox.Show(message, "Tin nhắn", MessageBoxButton.OK, MessageBoxImage.Asterisk, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
				});
			}
			catch (Exception)
			{
			}
		});
	}

	private void StartScreenSharing()
	{
		try
		{
			if (screenCaptureService == null)
			{
				screenCaptureService = new ScreenCaptureService(_mainSocket);
			}
			screenCaptureService.StartScreenSharing();
			_mainWindow.Dispatcher.Invoke(delegate
			{
				_mainWindow.UpdateApplicationStatus("Đang chia sẻ màn hình");
			});
		}
		catch (Exception)
		{
		}
	}

	private void StopScreenSharing()
	{
		try
		{
			screenCaptureService?.StopScreenSharing();
			_mainWindow.Dispatcher.Invoke(delegate
			{
				_mainWindow.UpdateApplicationStatus("Đã dừng chia sẻ màn hình");
			});
		}
		catch (Exception)
		{
		}
	}

	private void SetFrameRate(int fps)
	{
		try
		{
			if (screenCaptureService == null)
			{
			}
		}
		catch (Exception)
		{
		}
	}

	private void SetQuality(int quality)
	{
		try
		{
			if (screenCaptureService == null)
			{
			}
		}
		catch (Exception)
		{
		}
	}

	private void OpenVideoFolder()
	{
		try
		{
			long value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string newFolderPath = Path.Combine(folderPath, $"{value}_video");
			if (!Directory.Exists(newFolderPath))
			{
				Directory.CreateDirectory(newFolderPath);
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = "\"" + newFolderPath + "\"",
				UseShellExecute = true
			});
			Task.Run(delegate
			{
				try
				{
					string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					string text = Path.Combine(folderPath2, "RikkeiEducation", "ScreenRecordings");
					List<string> list = (from f in Directory.GetFiles(text, "*.wmv", SearchOption.AllDirectories)
						where f.Contains("screen_recording_")
						select f).ToList();
					int num = 0;
					foreach (string item in list)
					{
						try
						{
							string relativePath = Path.GetRelativePath(text, item);
							string text2 = Path.Combine(newFolderPath, relativePath);
							string directoryName = Path.GetDirectoryName(text2);
							if (!Directory.Exists(directoryName))
							{
								Directory.CreateDirectory(directoryName);
							}
							File.Copy(item, text2, overwrite: true);
							num++;
						}
						catch (Exception)
						{
						}
					}
				}
				catch (Exception)
				{
				}
			});
		}
		catch (Exception)
		{
		}
	}

	private void CleanVideoFolder()
	{
		try
		{
			if (_mainWindow.aiDetect != null)
			{
				_mainWindow.aiDetect.ForceStopVideoRecording();
			}
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			string path = Path.Combine(folderPath, "RikkeiEducation", "ScreenRecordings");
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch (Exception)
		{
		}
	}
}
