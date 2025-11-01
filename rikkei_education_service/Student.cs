using System;
using System.Text.Json.Serialization;

namespace rikkei_education_service;

public class Student
{
	[JsonPropertyName("studentId")]
	public int StudentId { get; set; }

	[JsonPropertyName("fullName")]
	public string FullName { get; set; }

	[JsonPropertyName("email")]
	public string Email { get; set; }

	[JsonPropertyName("avatar")]
	public string Avatar { get; set; }

	[JsonPropertyName("dob")]
	public string Dob { get; set; }

	[JsonPropertyName("studentCode")]
	public string StudentCode { get; set; }

	[JsonPropertyName("phone")]
	public string Phone { get; set; }

	[JsonPropertyName("systemId")]
	public int SystemId { get; set; }

	[JsonPropertyName("wifiBSSID")]
	public string WifiBSSID { get; set; }

	[JsonPropertyName("wifiSSID")]
	public string WifiSSID { get; set; }

	[JsonPropertyName("os")]
	public string Os { get; set; }

	public string GetFormattedDob()
	{
		if (DateTime.TryParse(Dob, out var result))
		{
			return result.ToString("dd/MM/yyyy");
		}
		return Dob;
	}
}
