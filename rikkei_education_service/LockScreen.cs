using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace rikkei_education_service;

public partial class LockScreen : Window, IComponentConnector
{
	private DispatcherTimer timer;

	private DateTime lockStartTime;

	public LockScreen()
	{
		InitializeComponent();
		InitializeTimer();
	}

	private void InitializeTimer()
	{
		lockStartTime = DateTime.Now;
		timer = new DispatcherTimer();
		timer.Interval = TimeSpan.FromSeconds(1.0);
		timer.Tick += Timer_Tick;
		timer.Start();
	}

	private void Timer_Tick(object sender, EventArgs e)
	{
		TimeSpan timeSpan = DateTime.Now - lockStartTime;
		TimerText.Text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
	}

	public void Unlock()
	{
		timer?.Stop();
		Close();
	}
}
