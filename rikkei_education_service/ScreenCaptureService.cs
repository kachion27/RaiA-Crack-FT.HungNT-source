using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace rikkei_education_service;

public class ScreenCaptureService
{
	private bool isStreaming = false;

	private int frameRate = 10;

	private int quality = 80;

	private MainSocket mainSocket;

	public ScreenCaptureService(MainSocket _mainSocket)
	{
		mainSocket = _mainSocket;
	}

	public async Task StartScreenSharing()
	{
		try
		{
			isStreaming = true;
			while (isStreaming)
			{
				using (Bitmap bitmap = CaptureScreen())
				{
					byte[] imageBytes = ConvertToBytes(bitmap);
					await SendFrame(imageBytes);
				}
				await Task.Delay(1000 / frameRate);
			}
		}
		catch (Exception)
		{
		}
	}

	public void StopScreenSharing()
	{
		isStreaming = false;
	}

	public void Dispose()
	{
		StopScreenSharing();
	}

	private Bitmap CaptureScreen()
	{
		try
		{
			Rectangle bounds = Screen.PrimaryScreen.Bounds;
			Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
			}
			return bitmap;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private byte[] ConvertToBytes(Bitmap bitmap)
	{
		try
		{
			using MemoryStream memoryStream = new MemoryStream();
			ImageCodecInfo encoder = GetEncoder(ImageFormat.Jpeg);
			EncoderParameters encoderParameters = new EncoderParameters(1);
			encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
			bitmap.Save(memoryStream, encoder, encoderParameters);
			return memoryStream.ToArray();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private ImageCodecInfo GetEncoder(ImageFormat format)
	{
		ImageCodecInfo[] imageDecoders = ImageCodecInfo.GetImageDecoders();
		ImageCodecInfo[] array = imageDecoders;
		foreach (ImageCodecInfo imageCodecInfo in array)
		{
			if (imageCodecInfo.FormatID == format.Guid)
			{
				return imageCodecInfo;
			}
		}
		return null;
	}

	private async Task SendFrame(byte[] imageBytes)
	{
		try
		{
			if (imageBytes != null && imageBytes.Length != 0)
			{
				var frameData = new
				{
					type = "screen_frame",
					data = Convert.ToBase64String(imageBytes),
					timestamp = DateTime.Now,
					size = imageBytes.Length
				};
				await mainSocket.SendObjectAsync("screen_frame", frameData);
			}
		}
		catch (Exception)
		{
		}
	}
}
