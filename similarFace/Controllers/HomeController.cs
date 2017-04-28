using ImageResizer;
using Microsoft.Azure;
using Microsoft.ProjectOxford.Face;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using similarFace.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace similarFace.Controllers
{
    public class HomeController : Controller
    {
        //upload user picture
        public ActionResult index(string userPhotoURL = null, int similarPercent = -1, string celebrityFaceId = null, string error = null)
        {
            // Pass a list of blob URIs in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer containerCelebrity = client.GetContainerReference("celebrity");

            List<CelebrityInfo> Celebrityblobs = new List<CelebrityInfo>();
            List<UserInfo> Userblobs = new List<UserInfo>();

            int percent = -1;
            if (!String.IsNullOrEmpty(celebrityFaceId) && !String.IsNullOrEmpty(userPhotoURL) && similarPercent != -1 && String.IsNullOrEmpty(error))
            {
                foreach (IListBlobItem item in containerCelebrity.ListBlobs())
                {
                    var blob = item as CloudBlockBlob;

                    if (blob != null)
                    {
                        blob.FetchAttributes(); // Get blob metadata
                        if (blob.Metadata.ContainsKey("faceId")) {
                        
                            var persistedFaceId = blob.Metadata["faceId"];
                            if (celebrityFaceId == persistedFaceId)
                            {
                                Celebrityblobs.Add(new CelebrityInfo()
                                {
                                    ImageUri = blob.Uri.ToString(),
                                    ThumbnailUri = blob.Uri.ToString().Replace("/celebrity/", "/thumbnails/"),
                                    SimilarPercent = similarPercent
                                });

                                Userblobs.Add(new UserInfo()
                                {
                                    ImageUri = userPhotoURL,
                                    ThumbnailUri = userPhotoURL.Replace("/photos/", "/userthumbnails/"),
                                });

                                percent = similarPercent;
                                break;
                            }
                        }
                    }
                }
                
            }
            if (!String.IsNullOrEmpty(error))
            {
                ViewBag.error = error;
            }
            else
            {
                ViewBag.error = "";
            }


            if (percent != -1)
            {
                ViewBag.percent = "Similar percentage is " + percent + "%.";
            }
            else
            {
                ViewBag.percent = "";
            }


            ViewBag.Userblobs = Userblobs.ToArray();
            ViewBag.Celebrityblobs = Celebrityblobs.ToArray();
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> UploadUserPic(HttpPostedFileBase file)
        {
            int percent = -1;
            string persistedFaceId = null;
            string photoUriStr = null;
            string errorMsg = null;
            if (file != null && file.ContentLength > 0)
            {
                // Make sure the user selected an image file
                if (!file.ContentType.StartsWith("image"))
                {
                    TempData["Message"] = "Only image files may be uploaded";
                }
                else
                {
                    // Save the original image in the "photos" container
                    CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                    CloudBlobClient client = account.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("photos");
                    CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                    await photo.UploadFromStreamAsync(file.InputStream);
                    file.InputStream.Seek(0L, SeekOrigin.Begin);

                    // Generate a thumbnail and save it in the "thumbnails" container
                    using (var outputStream = new MemoryStream())
                    {
                        var settings = new ResizeSettings { MaxWidth = 192, Format = "png" };
                        ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                        outputStream.Seek(0L, SeekOrigin.Begin);
                        container = client.GetContainerReference("userthumbnails");
                        CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await thumbnail.UploadFromStreamAsync(outputStream);
                    }

                    // Submit the image to Azure's Computer Vision API
                    FaceServiceClient faceServiceClient = new FaceServiceClient(CloudConfigurationManager.GetSetting("SubscriptionKey"));
                    photoUriStr = photo.Uri.ToString();
                    var faces = await faceServiceClient.DetectAsync(photoUriStr);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();
                    if (faceIds.Count() == 1)
                    {
                        var results = await faceServiceClient.FindSimilarAsync(faceIds[0], "4976", FindSimilarMatchMode.matchFace);
                        percent = (int)(results[0].Confidence * 100);
                        persistedFaceId = results[0].PersistedFaceId.ToString();
                        errorMsg = null;
                    }
                    else if (faceIds.Count() == 0) {
                        errorMsg = "No face detected in the image!";
                    } else {
                        errorMsg = "There is more than 1 face in the image or in the specified targetFace area!";
                    }
                }
            }

            // redirect back to the index action to show the form once again
            return RedirectToAction("Index", new { userPhotoURL = photoUriStr, similarPercent = percent, celebrityFaceId = persistedFaceId, error = errorMsg });
        }

        //upload celebrity pictures
        public ActionResult UploadCelebrityPic(string error = null)
        {
            // Pass a list of blob URIs in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("celebrity");
            List<CelebrityInfo> blobs = new List<CelebrityInfo>();

            foreach (IListBlobItem item in container.ListBlobs())
            {
                var blob = item as CloudBlockBlob;

                if (blob != null)
                {
                    blob.FetchAttributes(); // Get blob metadata
                    if (blob.Metadata.ContainsKey("faceId"))
                    {
                        blobs.Add(new CelebrityInfo()
                        {
                            ImageUri = blob.Uri.ToString(),
                            ThumbnailUri = blob.Uri.ToString().Replace("/celebrity/", "/thumbnails/")
                        });
                    }
                }
            }
            if (!String.IsNullOrEmpty(error))
            {
                ViewBag.error = error;
            }
            else {
                ViewBag.error = "";
            }
            
            ViewBag.Blobs = blobs.ToArray();
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> UploadCelebrityPic(HttpPostedFileBase file)
        {
            
            string persistedFaceId = null;
            string photoUriStr = null;
            string errorMsg = null;

            if (file != null && file.ContentLength > 0)
            {
                // Make sure the user selected an image file
                if (!file.ContentType.StartsWith("image"))
                {
                    TempData["Message"] = "Only image files may be uploaded";
                }
                else
                {
                    // Save the original image in the "photos" container
                    CloudStorageAccount account = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                    CloudBlobClient client = account.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("celebrity");
                    CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                    await photo.UploadFromStreamAsync(file.InputStream);
                    file.InputStream.Seek(0L, SeekOrigin.Begin);

                    // Generate a thumbnail and save it in the "thumbnails" container
                    using (var outputStream = new MemoryStream())
                    {
                        var settings = new ResizeSettings { MaxWidth = 192, Format = "png" };
                        ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                        outputStream.Seek(0L, SeekOrigin.Begin);
                        container = client.GetContainerReference("thumbnails");
                        CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await thumbnail.UploadFromStreamAsync(outputStream);
                    }

                    // Submit the image to Azure's Computer Vision API
                    FaceServiceClient faceServiceClient = new FaceServiceClient(CloudConfigurationManager.GetSetting("SubscriptionKey"));
                    photoUriStr = photo.Uri.ToString();
                    try {
                        var result = await faceServiceClient.AddFaceToFaceListAsync("4976", photoUriStr);
                        persistedFaceId = result.PersistedFaceId.ToString();

                        // Record the image faceID in blob metadata
                        photo.Metadata.Add("faceId", persistedFaceId);
                        await photo.SetMetadataAsync();
                    }
                    catch (Exception ex)
                    {
                        errorMsg = ((Microsoft.ProjectOxford.Face.FaceAPIException)ex).ErrorMessage;
                    }
 
                }
            }

            // redirect back to the index action to show the form once again
            return RedirectToAction("UploadCelebrityPic", new { error = errorMsg });
        }



    }
}