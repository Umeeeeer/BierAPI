using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BierAPI.Model
{
    class QueueStorageMessage
    {
        public float Longtitude { get; set; }
        public float Latitude { get; set; }
        public string BlobName { get; set; }
        public string BlobContainerReference { get; set; }

        public QueueStorageMessage(float longtitude, float latitude, string blobname, string blobcontainerreference)
        {
            this.Longtitude = longtitude;
            this.Latitude = latitude;
            this.BlobName = blobname;
            this.BlobContainerReference = blobcontainerreference;
        }
    }
}
