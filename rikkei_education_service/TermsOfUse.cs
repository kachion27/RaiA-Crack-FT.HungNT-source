using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;

namespace rikkei_education_service;

public partial class TermsOfUse : Window, IComponentConnector
{
	public TermsOfUse()
	{
		InitializeComponent();
	}

	private void AcceptButton_Click(object sender, RoutedEventArgs e)
	{
		SaveTermsAcceptance();
		LoadNextWindow();
	}

	private void DeclineButton_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
	}

	private void SaveTermsAcceptance()
	{
		try
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			string text = Path.Combine(folderPath, "RikkeiEducationService");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			var value = new
			{
				accepted = true,
				acceptedAt = DateTime.Now,
				version = "1.0.0"
			};
			string path = Path.Combine(text, "terms_of_use_accept.json");
			string contents = JsonSerializer.Serialize(value, new JsonSerializerOptions
			{
				WriteIndented = true,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			});
			File.WriteAllText(path, contents, Encoding.UTF8);
			MessageBox.Show("Đã chấp nhận điều khoản sử dụng!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			throw new Exception("Không thể lưu điều khoản: " + ex.Message);
		}
	}

	private void LoadNextWindow()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string path = Path.Combine(folderPath, "RikkeiEducationService", "student.json");
		if (File.Exists(path))
		{
			MainWindow mainWindow = new MainWindow();
			mainWindow.Show();
		}
		else
		{
			LoginWindow loginWindow = new LoginWindow();
			loginWindow.Show();
		}
		Close();
	}
}
