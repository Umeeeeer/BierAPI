using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using GoogleMaps.LocationServices;
using System;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Queue;
using BierAPI.Model;
using System.Text;

namespace BierAPI
{
    public static class GetMap
    {
        [FunctionName("GetMap")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            try
            {
                //Get streetname from query parameter
                string city = req.GetQueryNameValuePairs()
                    .FirstOrDefault(q => string.Compare(q.Key, "city", true) == 0)
                    .Value;

                if (city == null)
                {
                    dynamic data = await req.Content.ReadAsAsync<object>();
                    city = data?.city;
                }

                //Get country from query parameter
                string country = req.GetQueryNameValuePairs()
                    .FirstOrDefault(q => string.Compare(q.Key, "country", true) == 0)
                    .Value;

                if (country == null)
                {
                    dynamic data = await req.Content.ReadAsAsync<object>();
                    country = data?.country;
                }

                //Als er wel een stad en land is ingevuld
                if (country != null && city != null)
                {
                    //Google api key ophalen en API url opstellen om te checken of de combinatie bestaat en de long en lat op te halen.
                    string googleapikey = Environment.GetEnvironmentVariable("GoogleAPIkey");
                    var url = String.Format("https://maps.googleapis.com/maps/api/geocode/json?address={0},+{1}&key={2}", city, country, googleapikey);
                    log.Info("Google api link: " + url);
                    using (var client = new HttpClient())
                    {
                        //Wachten op een response
                        client.BaseAddress = new Uri(url);
                        HttpResponseMessage response = await client.GetAsync(url);

                        //Als de response een sucess was
                        if (response.IsSuccessStatusCode)
                        {
                            //String ophalen en omzetten naar een json object
                            string strResult = await response.Content.ReadAsStringAsync();
                            dynamic googlemapsobject = JsonConvert.DeserializeObject<dynamic>(strResult);

                            //De status uit het json object lezen
                            string status = (string)googlemapsobject.status;

                            //Als er geen resultaten waren voor de opgegeven combinatie
                            if (status == "ZERO_RESULTS")
                            {
                                return req.CreateErrorResponse(HttpStatusCode.NotFound, "Your combination of city and country could not be found, please enter a valid city and country!");
                            }

                            //Als er wel resultaten waren
                            else
                            {
                                //Geocoordinaten ophalen voor de locatie
                                string latitude = googlemapsobject.results[0].geometry.location.lat;
                                string longtitude = googlemapsobject.results[0].geometry.location.lng;

                                //Bloblocatie opstellen
                                string blobname = String.Format("Generatedmap-{0},{1}-{2}.png", city, country, DateTime.Now.ToFileTime());
                                string blobcontainerreference = "mapblob";
                                log.Info(Environment.GetEnvironmentVariable("StorageConnectionString"));
                                CloudStorageAccount account = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));
                                string blobUrl = account.BlobStorageUri.PrimaryUri.AbsoluteUri + blobcontainerreference + "/" + blobname;
                                log.Info("bloburl= " + blobUrl);

                                //Object voor het doorsturen naar de queue aanmaken
                                QueueStorageMessage message = new QueueStorageMessage(longtitude, latitude, blobname, blobcontainerreference);

                                //Als json string doorsturen naar de queue storage
                                string json = JsonConvert.SerializeObject(message);
                                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageConnectionString"));
                                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                                CloudQueue queue = queueClient.GetQueueReference("bierapi-queue");
                                queue.CreateIfNotExists();
                                CloudQueueMessage queueumessage = new CloudQueueMessage(json);
                                queue.AddMessage(queueumessage);

                                var myObj = new { status = "request made", url = blobUrl, message = "Your requested map will be available at the url shortly" };
                                var jsonToReturn = JsonConvert.SerializeObject(myObj);

                                return new HttpResponseMessage(HttpStatusCode.OK)
                                {
                                    Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                                };
                            }
                        }
                    }
                }

                else
                {
                    var myObj = new { status = "ERROR", message = "Please use a valid country name and city name!" };
                    var jsonToReturn = JsonConvert.SerializeObject(myObj);

                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(jsonToReturn, Encoding.UTF8, "application/json")
                    };
                }
            }

            catch(Exception ex)
            {
                log.Info("Exception" + ex.Message + " " + ex.InnerException);
            }

            return null;
        }
    }
}

