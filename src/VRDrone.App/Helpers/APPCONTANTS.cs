using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRDrone.App
{
   public  class APPCONTANTS
    {
        //cognitive service
        public const string MQTT_SERVER = "34.87.47.181";//"13.76.156.239";//"23.98.70.88";//"cloud.makestro.com"; //"cloud.makestro.com";
        public const string MQTT_USER = "mqtt_01";//"loradev_mqtt";
        public const string MQTT_PASS = "MVBnn0012";//"test123";
        public const string BING_API_KEY = "7cbea11a8b5344f08b2b5db408f91ed4";
        public const string COMPUTERVISION_KEY = "fab29c974d2d4006a5ed0d05ca503196";// "274ebc67bd4441de91732ef1aaa3f00a";//"fab29c974d2d4006a5ed0d05ca503196";
        public const string LUIS_APP_ID = "0aa11a64-a01f-40b1-afb6-2daffaabadc1";
        public const string LUIS_SUB_KEY = "0d97be5a9b63419b884977611ccdba1f";
        public const string EMOTION_KEY = "cc22aa743b0340cdb9fb9094cdaadf53";
        public const string FACE_KEY = "a068e60df8254cc5a187e3e8c644f316";//"a068e60df8254cc5a187e3e8c644f316";
        public const string TEXTANALYSIS_KEY = "d850796f81484952a3fe3c6bfcaac5ba";
        //socmed
        public const string Twitter_ConsumerKey = "MKbCFe1z8IYVyjMJsSnvA";
        public const string Twitter_ConsumerSecret = "zmAQFP52CTO1ZYzoFLMnLxELeyt7iYIrCVs59mFkQ";
        public const string Twitter_AccessToken = "15628267-iFdsMnMVe6qQY6254s2PMSGFdR7a8Cf2h9kh7omnm";
        public const string Twitter_AccessTokenSecret = "Uz9USTVa4qSh3tRjUpyvx3rCLEd1Nz6wbP0QDikZKWQ";
        public const string ApiUrl = "http://gravicodeabsensiweb.azurewebsites.net/api/TonyVisions";
        public const int IntervalTimerMin = 30;
        public const string BlobConnString = "DefaultEndpointsProtocol=https;AccountName=storagemurahaje;AccountKey=NU2f/5suzFgLyGYplR6ydXQ+6L8STLCRviDqJf+MS8bVWsO3L5VWFK3qaUltdPNwdd092st0eJWQIBvLI0WI1A==;EndpointSuffix=core.windows.net";// "DefaultEndpointsProtocol=https;AccountName=bmspace;AccountKey=TK7Yz24n8Mb89qzI2Vrwu0xCLW/EuB7fc1EjM2IcRHZHJXUCgIuaqOxGszOKE9uADIRY7XBJFfF0GWIX9/hKUw==;EndpointSuffix=core.windows.net";
        public const string AzureIoTCon = "HostName=FreeDeviceHub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=pGREIqFsT9rGgDkGJP3K5Vkrg5zmTnNZAxNeqWpT4UM=";
    }

    public class TonyVisionObj
    {
        public int id { get; set; }
        public string description { get; set; }
        public DateTime tanggal { get; set; }
        public string tags { get; set; }
        public string adultContent { get; set; }
        public string adultScore { get; set; }
        public int facesCount { get; set; }
        public string facesDescription { get; set; }
    }

}
