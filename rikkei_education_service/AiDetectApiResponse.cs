using System.Collections.Generic;

namespace rikkei_education_service;

public class AiDetectApiResponse
{
	public List<AiDetectData> data { get; set; }

	public int total { get; set; }
}
