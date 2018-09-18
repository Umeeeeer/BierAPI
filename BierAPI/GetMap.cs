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

namespace BierAPI
{
    public static class GetMap
    {
        [FunctionName("GetMap")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            var latitude = "";
            var longtitude = "";

            Random random = new Random();

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
                // Get request body
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

            else
            {
                string address = String.Format("{0}, {1}", city, country);
                string googleapikey = Environment.GetEnvironmentVariable("GoogleAPIkey");

                var url = String.Format("https://maps.googleapis.com/maps/api/geocode/json?address={0},+{1}&key={2}", city, country, googleapikey);

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(url);

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string strResult = await response.Content.ReadAsStringAsync();
                        dynamic obj = JsonConvert.DeserializeObject<dynamic>(strResult);

                        string status = (string)obj.status;

                        if(status == "ZERO_RESULTS")
                        {
                            return req.CreateResponse(HttpStatusCode.NotFound, "Please enter a valid country and city");
                        }

                        else
                        {
                            latitude = obj.results[0].geometry.location.lat;
                            longtitude = obj.results[0].geometry.location.lng;
                            string azuremapskey = Environment.GetEnvironmentVariable("AzuremapsKey");

                            using (var mapclient = new HttpClient())
                            {
                                var url2 = String.Format("https://atlas.microsoft.com/map/static/png?subscription-key={0}&api-version=1.0&center={1},{2}",azuremapskey, longtitude, latitude);

                                mapclient.BaseAddress = new Uri(url2);
                                HttpResponseMessage response2 = await client.GetAsync(url2);

                                if(response2.IsSuccessStatusCode)
                                {
                                    System.IO.Stream responseStream = await response2.Content.ReadAsStreamAsync();

                                    // Retrieve storage account from connection string.
                                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=kanikhierbierdr92ec;AccountKey=amIPMHOWpsR9/flak1yiJa/lZ+lktu4rV9ipSH2YHzFYbpNzHXlzMYCanR+YV0Pk2BKRbppxbDyO7HR3kjmXLg==;EndpointSuffix=core.windows.net");

                                    // Create the blob client.
                                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                                    // Retrieve a reference to a container.
                                    CloudBlobContainer container = blobClient.GetContainerReference("mapblob");
                                    // Create the container if it doesn't already exist.
                                    await container.CreateIfNotExistsAsync();

                                    // create a blob in the path of the <container>/email/guid
                                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(String.Format("Mapgeneratedfrom-{0},{1}-at-{2}.png", country, city, DateTime.Now.ToFileTime()));

                                    await blockBlob.UploadFromStreamAsync(responseStream);
                                }
                            }
                        }
                    }

                    else
                    {
                        return req.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "Service currently unavailable, please try again later!");
                    }
                }

                return req.CreateResponse(HttpStatusCode.OK, "This is your link: https://kanikhierbierdr92ec.blob.core.windows.net/mapblob/" + String.Format("Mapgeneratedfrom-{0},{1}-at-{2}.png", country, city, DateTime.Now.ToFileTime()));
            }
        }
    }
}
