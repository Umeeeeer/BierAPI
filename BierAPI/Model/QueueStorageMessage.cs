using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BierAPI.Model
{
    class QueueStorageMessage
    {
        public string Longtitude { get; set; }
        public string Latitude { get; set; }
        public string BlobName { get; set; }
        public string BlobContainerReference { get; set; }

        public QueueStorageMessage(string longtitude, string latitude, string blobname, string blobcontainerreference)
        {
            this.Longtitude = longtitude;
            this.Latitude = latitude;
            this.BlobName = blobname;
            this.BlobContainerReference = blobcontainerreference;
        }
    }
}
