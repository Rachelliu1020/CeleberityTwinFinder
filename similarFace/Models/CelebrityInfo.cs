using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace similarFace.Models
{
    public class CelebrityInfo
    {
        public string ImageUri { get; set; }
        public string ThumbnailUri { get; set; }
        public int SimilarPercent { get; set; }
    }
}