using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace PinAndMeetService.Helpers {
    public static class ImageHelpers {
        private const int MAX_HEIGHT_OR_WIDTH = 600;
        private const int MAX_IMAGE_SIZE = 1024 * 10; // kB  -> 10M
        private const string STORAGE_URI = "https://4ph.blob.core.windows.net"; // [Input here]
        private const string IMAGE_URL_BASE = "https://4ph.blob.core.windows.net/img/"; // [Input here]
        private const string STORAGE_ACCOUNT_NAME = "[Input here]";
        private const string STORAGE_ACCOUNT_PRIMARY_ACCESS_KEY = "[Input here]";
        private const string STORAGE_CONTAINER_NAME = "img";


        private static Random rand = new Random();


        public static string UploadAndProcessAvatarImage(HttpPostedFileBase file, int maxWidth = MAX_HEIGHT_OR_WIDTH) {
            if (file == null) return null;
            string fileName = replaceInvalidCharacters(Path.GetFileName(file.FileName));
            string head;
            string tail;
            splitFileName(fileName, out head, out tail);
            if (tail == "") tail = ".jpg";

            // Check is valid file type
            bool valid = isValidTail(tail);
            if (!valid) return "-1";

            // Check is valid size
            double size = (double)file.ContentLength / 1024.0; // kB
            if (size > MAX_IMAGE_SIZE) return "-2";

            string newFileName = head + "-" + rand.Next(1000, 9999).ToString();

            CloudBlobClient blobClient = new CloudBlobClient(new Uri(STORAGE_URI), new StorageCredentials(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_PRIMARY_ACCESS_KEY));

            CloudBlobContainer container = blobClient.GetContainerReference(STORAGE_CONTAINER_NAME);

            using (Stream inputStream = file.InputStream) {
                var imageStream = new MemoryStream();
                file.InputStream.CopyTo(imageStream); // For some reason needs to be copied
                var imageStreamPure = new MemoryStream();

                Image img = System.Drawing.Image.FromStream(imageStream);

                // Prevent rotation in iOs devices
                if (tail.ToUpper() == ".JPG" || tail.ToUpper() == ".JPEG") {

                    var orientation = 0;
                    try { orientation = (int)img.GetPropertyItem(274).Value[0]; } catch {} // 0 if not found
                    switch (orientation) {
                        case 1:
                            // No rotation required.
                            break;
                        case 2:
                            img.RotateFlip(RotateFlipType.RotateNoneFlipX);
                            break;
                        case 3:
                            img.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            break;
                        case 4:
                            img.RotateFlip(RotateFlipType.Rotate180FlipX);
                            break;
                        case 5:
                            img.RotateFlip(RotateFlipType.Rotate90FlipX);
                            break;
                        case 6:
                            img.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            break;
                        case 7:
                            img.RotateFlip(RotateFlipType.Rotate270FlipX);
                            break;
                        case 8:
                            img.RotateFlip(RotateFlipType.Rotate270FlipNone);
                            break;
                    }

                    img.Save(imageStreamPure, ImageFormat.Jpeg);
                } else {
                    imageStreamPure = imageStream;
                }

                CloudBlockBlob blockBlob3 = container.GetBlockBlobReference(newFileName + tail);
                var imageStream3 = getResizedPictureStream(imageStreamPure, MAX_HEIGHT_OR_WIDTH, tail );
                saveBlob(file, blockBlob3, imageStream3);
            }
            return newFileName + tail;

        }

        private static string replaceInvalidCharacters(string originalFileName) {
            return originalFileName.Replace(' ', '_').Replace("%20", "_").Replace('%', '-');
        }

        private static void splitFileName(string fileName, out string head, out string tail) {
            head = "";
            tail = "";
            if (fileName.IndexOf('.') == -1) {
                head = fileName;
                return;
            }
            for (int i = fileName.Length - 1; i >= 0; i--) {
                if (fileName.Substring(i, 1) == ".") {
                    head = fileName.Substring(0, i);
                    tail = fileName.Substring(i, fileName.Length - i);
                    return;
                }
            }
        }

        private static bool isValidTail(string tail) {
            tail = tail.ToUpper();
            if (tail == ".JPG" || tail == ".JPEG" || tail == ".GIF" || tail == ".PNG") return true;
            return false;
        }
        // Scale image. Side can be horizontal or vertical, which is bigger
        // If makeBigger == true AND  original is smaller, stretch to be bigger
        // If maxWidth is given, maxSideLenght is not used
        private static MemoryStream getResizedPictureStream(MemoryStream originalImage, int maxSideLength, string fileType, bool makeBigger = false, int maxWidth = -1) {
            var result = new MemoryStream();

            Bitmap originalBitmap = new Bitmap(originalImage);

            double ration = 0;
            int height = 0;
            int width = 0;

            if (maxWidth == -1) {
                if (originalBitmap.Height > originalBitmap.Width) {
                    height = maxSideLength;
                    ration = (double)((double)maxSideLength / (double)originalBitmap.Height);
                    width = (int)(ration * (double)originalBitmap.Width);
                } else {
                    width = maxSideLength;
                    ration = (double)((double)maxSideLength / (double)originalBitmap.Width);
                    height = (int)(ration * (double)originalBitmap.Height);
                }
            } else {  // Only max widht is given
                width = maxWidth;
                ration = (double)((double)maxWidth / (double)originalBitmap.Width);
                height = (int)(ration * (double)originalBitmap.Height);
            }

            if (width > originalBitmap.Width && makeBigger == false) {
                width = originalBitmap.Width;
                height = originalBitmap.Height;
            }

            Bitmap newBitmap = new Bitmap(width, height);
            newBitmap = getResizedImage(originalBitmap, width, height);
            switch (fileType.ToUpper()) {
                case ".JPG":
                    newBitmap.Save(result, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;
                case ".JPEG":
                    newBitmap.Save(result, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;
                case ".GIF":
                    newBitmap.Save(result, System.Drawing.Imaging.ImageFormat.Gif);
                    break;
                case ".PNG":
                    newBitmap.Save(result, System.Drawing.Imaging.ImageFormat.Png);
                    break;
            }
            return result;
        }

        private static Bitmap getResizedImage(Bitmap image, int width, int height) {
            Bitmap resizedImage = new Bitmap(width, height);
            using (Graphics graphics = Graphics.FromImage(resizedImage)) {
                graphics.DrawImage(image, new Rectangle(0, 0, width, height), new Rectangle(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }
            return resizedImage;
        }

        // Save in azure blob
        private static void saveBlob(HttpPostedFileBase file, CloudBlockBlob blockBlob, MemoryStream imageStream) {
            imageStream.Position = 0;
            blockBlob.UploadFromStream(imageStream);
            blockBlob.Properties.ContentType = file.ContentType;
            blockBlob.SetProperties();
        }

        public static string GetAvatarFullUrl(string avatarFilename) {
            return IMAGE_URL_BASE + avatarFilename;
        }

    }
}