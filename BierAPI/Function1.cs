using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using GoogleMaps.LocationServices;
using System;

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
                var locationService = new GoogleLocationService();
                var point = locationService.GetLatLongFromAddress(address);
                var latitude = point.Latitude;
                var longtitude = point.Longitude;
                return req.CreateResponse(HttpStatusCode.OK, "Lat: " + latitude + "Long: " + longtitude);
            }
        }
    }
}
