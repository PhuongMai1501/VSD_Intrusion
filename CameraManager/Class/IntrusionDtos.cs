using Newtonsoft.Json;

namespace CameraManager.Class
{
    public class ApiBbox
    {
        [JsonProperty("x1")] public double X1 { get; set; }
        [JsonProperty("y1")] public double Y1 { get; set; }
        [JsonProperty("w")] public double W { get; set; }
        [JsonProperty("h")] public double H { get; set; }
    }

    public class ApiAction
    {
        [JsonProperty("action")] public string Action { get; set; }
        [JsonProperty("confidence")] public double Confidence { get; set; }
    }

    public class ApiPerson
    {
        [JsonProperty("track_id")] public int TrackId { get; set; }
        [JsonProperty("bbox")] public ApiBbox Bbox { get; set; }
        [JsonProperty("action")] public ApiAction Action { get; set; }
        [JsonProperty("buffer_size")] public int? BufferSize { get; set; }
    }

    public class MultiPersonDetectionResponse
    {
        [JsonProperty("persons")] public System.Collections.Generic.List<ApiPerson> Persons { get; set; }
    }
}

