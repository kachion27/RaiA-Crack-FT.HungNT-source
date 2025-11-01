namespace rikkei_education_service;

public class ProxyStatus
{
	public bool IsEnabled { get; set; }

	public string ProxyServer { get; set; }

	public string ProxyOverride { get; set; }

	public override string ToString()
	{
		return $"Proxy: {(IsEnabled ? "Enabled" : "Disabled")}, Server: {ProxyServer}, Override: {ProxyOverride}";
	}
}
