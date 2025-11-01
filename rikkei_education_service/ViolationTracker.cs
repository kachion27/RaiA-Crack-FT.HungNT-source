using System;
using System.Collections.Generic;

namespace rikkei_education_service;

public static class ViolationTracker
{
	private static readonly Dictionary<string, DateTime> lastViolationTime = new Dictionary<string, DateTime>();

	private static readonly object lockObject = new object();

	private const int VIOLATION_COOLDOWN_SECONDS = 30;

	public static bool CanSendReport(string keyword)
	{
		lock (lockObject)
		{
			if (!lastViolationTime.ContainsKey(keyword))
			{
				return true;
			}
			DateTime dateTime = lastViolationTime[keyword];
			return (DateTime.Now - dateTime).TotalSeconds >= 30.0;
		}
	}

	public static void MarkReportSent(string keyword)
	{
		lock (lockObject)
		{
			lastViolationTime[keyword] = DateTime.Now;
		}
	}

	public static void ClearAll()
	{
		lock (lockObject)
		{
			lastViolationTime.Clear();
		}
	}

	public static void ClearKeyword(string keyword)
	{
		lock (lockObject)
		{
			if (lastViolationTime.ContainsKey(keyword))
			{
				lastViolationTime.Remove(keyword);
			}
		}
	}
}
