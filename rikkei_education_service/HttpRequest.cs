namespace rikkei_education_service;

public class HttpRequest
{
	public string Method { get; set; }

	public string Url { get; set; }

	public string Host { get; set; }

	public string FullUrl { get; set; }

	public string[] Headers { get; set; }
}
