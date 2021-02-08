using System;
using System.Collections.Generic;

namespace Web1.Models
{
    public class FileContent
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public string ContentType { get; set; }
        public string ContentLength { get; set; }
        public DateTime LastModified { get; set; }
        public string Comment { get; set; }
    }
}