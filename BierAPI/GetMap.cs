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

namespace BierAPI
{
    public static class GetMap
    {
        [FunctionName("GetMap")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
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

            //Als er geen land is ingevuld direct een foutmelding geven
            if(country == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass the countryname on the query string or in the request body");
            }

            //Als er geen stad is ingevuld direct een foutmelding geven
            else if(city == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass the city on the query string or in the request body");
            }

            //Als er wel een stad en land is ingevuld
            else
            {
                //Google api key ophalen en API url opstellen om te checken of de combinatie bestaat en de long en lat op te halen.
                string googleapikey = Environment.GetEnvironmentVariable("GoogleAPIkey");
                var url = String.Format("https://maps.googleapis.com/maps/api/geocode/json?address={0},+{1}&key={2}", city, country, googleapikey);

                using (var client = new HttpClient())
                {
                    //Wachten op een response
                    client.BaseAddress = new Uri(url);
                    HttpResponseMessage response = await client.GetAsync(url);

                    //Als de response een sucess was
                    if(response.IsSuccessStatusCode)
                    {
                        //String ophalen en omzetten naar een json object
                        string strResult = await response.Content.ReadAsStringAsync();
                        dynamic googlemapsobject = JsonConvert.DeserializeObject<dynamic>(strResult);

                        //De status uit het json object lezen
                        string status = (string)googlemapsobject.status;
                        
                        //Als er geen resultaten waren voor de opgegeven combinatie
                        if(status == "ZERO_RESULTS")
                        {
                            return req.CreateErrorResponse(HttpStatusCode.NotFound, "Your combination of city and country could not be found, please enter a valid city and country!");
                        }

                        //Als er wel resultaten waren
                        else
                        {
                            //Geocoordinaten ophalen voor de locatie
                            float latitude = googlemapsobject.results[0].geometry.location.lat;
                            float longtitude = googlemapsobject.results[0].geometry.location.lng;

                            //Bloblocatie opstellen
                            string blobname = String.Format("Generatedmap-{0},{1}-{2}", city, country, DateTime.Now.ToFileTime());
                            string blobcontainerreference = "mapblob";
                            string bloburl = String.Format("https://kanikhierbierdr92ec.blob.core.windows.net/{0}/{1}", blobcontainerreference, blobname);
                            
                            //Object voor het doorsturen naar de queue aanmaken
                            QueueStorageMessage message = new QueueStorageMessage(longtitude, latitude, blobname, blobcontainerreference);

                            //Als json string doorsturen naar de queue storage
                            string json = JsonConvert.SerializeObject(message);
                            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("StorageAccountKey"));
                            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                            CloudQueue queue = queueClient.GetQueueReference("bierapi-queue");
                            queue.CreateIfNotExists();
                            CloudQueueMessage queueumessage = new CloudQueueMessage(json);
                            queue.AddMessage(queueumessage);

                            //Bevestig geven dat de request is gelukt en de link waar de map beschikbaar zal worden teruggeven
                            return req.CreateResponse(HttpStatusCode.OK, "Your request is being processed, your image will be ready at this link in a few seconds: " + bloburl);
                        }
                    }
                }
            }
            return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Please try again later!");
        }
    }
}
