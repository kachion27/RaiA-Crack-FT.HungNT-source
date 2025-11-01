using System.Net;

namespace rikkei_education_service;

public class HttpResponse
{
	public int StatusCode { get; set; }

	public WebHeaderCollection Headers { get; set; }

	public string Body { get; set; }
}
