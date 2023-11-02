using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace AutoMark
{
    class Ocr
    {
        // ===============================================================
        // 
        // SECURITY REMINDER      SECURITY REMINDER      SECURITY REMINDER
        //
        // This is probably TERRIBLE practice, keeping the key in the code
        // TODO: If this project at ALL takes off,
        //       CHANGE THE CODE AND CHANGE THE KEY!
        //
        // SECURITY REMINDER      SECURITY REMINDER      SECURITY REMINDER
        //
        // ===============================================================

        // Add your Computer Vision subscription key and endpoint to your environment variables.
        // static string subscriptionKey = Environment.GetEnvironmentVariable("COMPUTER_VISION_SUBSCRIPTION_KEY");
        static string subscriptionKey = "REPLACE_ME_WITH_A_VALID_KEY_HERE";

        // An endpoint should have a format like "https://westus.api.cognitive.microsoft.com"
        // static string endpoint = Environment.GetEnvironmentVariable("COMPUTER_VISION_ENDPOINT");
        static string endpoint = "REPLACE_ME_WITH_A_VALID_AZURE_ENDPOINT_URL";

        // the Batch Read method endpoint
        static string uriBase = endpoint + "/vision/v3.1/read/analyze";




        public Bitmap homework;
        public List<Rectangle> homeworkBoxes = new List<Rectangle>();
        public int maxOcrWidth;
        public int maxOcrHeight;

        public int collageEntrySeparation = 50;
        public int convincerSeparation = 2;
        public int homeworkBoxRemoval = 6; // TODO: TWEAK THIS! 4? 6?
        public float writingThreshold = 0.5F;

        private List<EntryInfoInternal> homeworkInfoInternal;



        // TODO: FEATURE! To avoid the boundaries of boxes messing things up,
        //       make them a really light colour when printing,
        //       and remove them based on pixel brightness after scanning

        /// <summary>
        /// Gets the text from the specified image file by using
        /// the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file with text.</param>
        public static async Task<JToken> ReadText(Bitmap image)
        {
            try
            {
                HttpClient client = new HttpClient();

                // Request headers.
                client.DefaultRequestHeaders.Add(
                    "Ocp-Apim-Subscription-Key", subscriptionKey);

                string url = uriBase;

                HttpResponseMessage response;

                // Two REST API methods are required to extract text.
                // One method to submit the image for processing, the other method
                // to retrieve the text found in the image.

                // operationLocation stores the URI of the second REST API method,
                // returned by the first REST API method.
                string operationLocation;

                // Reads the contents of the specified local image
                // into a byte array.
                // byte[] byteData = GetImageAsByteArray("test0.png");
                byte[] byteData = BitmapToPngAsByteArray(image);

                // Adds the byte array as an octet stream to the request body.
                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    // This example uses the "application/octet-stream" content type.
                    // The other content types you can use are "application/json"
                    // and "multipart/form-data".
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    // The first REST API method, Batch Read, starts
                    // the async process to analyze the written text in the image.
                    response = await client.PostAsync(url, content);
                }

                // The response header for the Batch Read method contains the URI
                // of the second method, Read Operation Result, which
                // returns the results of the process in the response body.
                // The Batch Read operation does not return anything in the response body.
                if (response.IsSuccessStatusCode)
                    operationLocation =
                        response.Headers.GetValues("Operation-Location").FirstOrDefault();
                else
                {
                    // Display the JSON error data.
                    string errorString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("\n\nResponse:\n{0}\n",
                        JToken.Parse(errorString).ToString());
                    return null;
                }

                // If the first REST API method completes successfully, the second 
                // REST API method retrieves the text written in the image.
                //
                // Note: The response may not be immediately available. Text
                // recognition is an asynchronous operation that can take a variable
                // amount of time depending on the length of the text.
                // You may need to wait or retry this operation.
                //
                // This example checks once per second for ten seconds.
                string contentString;
                int i = 0;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    response = await client.GetAsync(operationLocation);
                    contentString = await response.Content.ReadAsStringAsync();
                    ++i;
                }
                while (i < 60 && contentString.IndexOf("\"status\":\"succeeded\"") == -1);

                if (i == 60 && contentString.IndexOf("\"status\":\"succeeded\"") == -1)
                {
                    Console.WriteLine("\nTimeout error.\n");
                    return null;
                }

                // Display the JSON response.
                // Console.WriteLine("\nResponse:\n\n{0}\n", JToken.Parse(contentString).ToString());

                return JToken.Parse(contentString);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e.Message);
                return null;
            }
        }

        private static byte[] BitmapToPngAsByteArray(Bitmap image)
        {
            byte[] result = null;
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);
                result = stream.ToArray();
            }
            return result;
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        public static byte[] GetImageAsByteArray(string imageFilePath)
        {
            // Open a read-only file stream for the specified file.
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                // Read the file's contents into a byte array.
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }

        // Here's how we're gonna do things.
        // 1. We get a bunch of cutouts of the cells where things could be entered.
        // 2. We paste them all in a giant collage containing all of them.
        // 3. Each number has convincer.png pasted to the left/right.
        //    This makes the machine learning magic more convinced we're dealing with numbers.

        public async Task<List<EntryInfo>> RunOcr()
        {
            // Keep track of collage info, etc...
            homeworkInfoInternal = new List<EntryInfoInternal>();

            foreach (Rectangle coord in homeworkBoxes)
            {
                EntryInfoInternal entry = new EntryInfoInternal();
                entry.mainInfo.homeworkCoords = coord;
                homeworkInfoInternal.Add(entry);
            }

            // This modifies homeworkInfoInternal to point to the appropriate collages and coordinates in them
            List<Bitmap> collages = GetCollagesOfEntries();

            // This modifies homeworkInfoInternal to contain the contents of the homework boxes
            await ReadTextOnCollages(collages);

            List<EntryInfo> result = new List<EntryInfo>();

            foreach (EntryInfoInternal entry in homeworkInfoInternal)
            {
                result.Add(entry.mainInfo);
            }

            return result;
        }

        private async Task ReadTextOnCollages(List<Bitmap> collages)
        {
            int i = 0;
            foreach (Bitmap collage in collages)
            {
                // TODO: DEBUG INFO, COMMENT OUT LATER
                collage.Save("collage" + i + ".png", ImageFormat.Png);
                i++;

                //Console.WriteLine("Getting OCR result...");
                JToken jToken = await ReadText(collage);

                //TODO: DEBUG INFO, COMMENT OUT LATER
                Console.WriteLine(jToken.ToString());

                if (jToken.Value<string>("status") != "succeeded")
                {
                    Console.WriteLine("READ FAILED...");
                    continue;
                }

                JToken analyzeResult = jToken.Value<JToken>("analyzeResult");
                JArray readResults = analyzeResult.Value<JArray>("readResults");
                JArray lines = readResults[0].Value<JArray>("lines"); // Separated by pages?, only submitted one anyways.

                // TODO: So as it turns out, multiple values might be put on the same "line".
                //       Need to break them up on a per "word" basis.
                foreach (JToken line in lines)
                {
                    JArray words = line.Value<JArray>("words");

                    foreach (JToken word in words)
                    {
                        int[] boundingBox = word.Value<JArray>("boundingBox").Select(x => (int)x).ToArray();
                        string text = word.Value<string>("text");

                        Point point1 = new Point(boundingBox[0], boundingBox[1]);
                        Point point2 = new Point(boundingBox[2], boundingBox[3]);
                        Point point3 = new Point(boundingBox[4], boundingBox[5]);
                        Point point4 = new Point(boundingBox[6], boundingBox[7]);

                        // Check which entry this corresponds to
                        EntryInfoInternal entry = null;
                        foreach (EntryInfoInternal test in homeworkInfoInternal)
                        {
                            if (test.collage == collage
                                && test.collageCoords.Contains(point1)
                                && test.collageCoords.Contains(point2)
                                && test.collageCoords.Contains(point3)
                                && test.collageCoords.Contains(point4))
                            {
                                entry = test;
                                break;
                            }
                        }

                        // I'd imagine this should never happen, BUT you never know
                        if (entry == null)
                        {
                            continue;
                        }

                        // TODO: You know, this may or may not end badly if the entire chunk isn't read in one go,
                        //       and there happens to be a 34 in the answer.

                        // TODO: There may or may not also be problems with this being broken up into multiple sections
                        //       and them being read in the wrong order.
                        if (text.StartsWith("34"))
                        {
                            text = text.Remove(0, 2);
                        }

                        if (text.EndsWith("34"))
                        {
                            text = text.Remove(text.Length - 2, 2);
                        }

                        // Remove whitespace. You never know.
                        text = string.Concat(text.Where(c => !Char.IsWhiteSpace(c)));

                        // TODO: Do other sanitization. Replace commas with periods. etc...

                        entry.mainInfo.ocrResult += text;
                    }
                }
            }
        }

        // TODO: Add check if collage not big enough to fit a single entry.
        //       If that's the case, things will end badly.
        private List<Bitmap> GetCollagesOfEntries()
        {
            List<Bitmap> collages = new List<Bitmap>();

            int maxHomeworkBoxWidth = GetMaxHomeworkBoxWidth();
            int maxHomeworkBoxHeight = GetMaxHomeworkBoxHeight();

            Bitmap collage = new Bitmap(maxOcrWidth, maxOcrHeight);
            Bitmap convincer = new Bitmap("convincer.png");
            Rectangle convincerRectangle = new Rectangle(0, 0, convincer.Width, convincer.Height);

            // The picture is
            // Horizontal: (convincer) (convincer separation) (trim box trim) (convincer separation) (convincer) (collage separation)
            int collageEntryWidth = convincer.Width + convincerSeparation + maxHomeworkBoxWidth - 2 * homeworkBoxRemoval + convincerSeparation + convincer.Width;
            int collageEntryHeight = Math.Max(convincer.Height, maxHomeworkBoxHeight - 2 * homeworkBoxRemoval);

            // Actually create the collage.
            //using (Graphics graphics = Graphics.FromImage(collage))
            //{

            Graphics graphics = Graphics.FromImage(collage);

            // Color the collage white
            graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, collage.Width, collage.Height));

            int currentCollageEntryX = collageEntrySeparation;
            int currentCollageEntryY = collageEntrySeparation;

            foreach (EntryInfoInternal entry in homeworkInfoInternal)
            {
                // Check if there is enough horizontal and vertical space for this.
                if (collage.Width - collageEntrySeparation - currentCollageEntryX < collageEntryWidth)
                {
                    currentCollageEntryX = collageEntrySeparation;
                    currentCollageEntryY += collageEntryHeight + collageEntrySeparation;
                }

                // Completely out of space in this collage.
                if (collage.Height - collageEntrySeparation - currentCollageEntryY < collageEntryHeight)
                {
                    collages.Add(collage);
                    collage = new Bitmap(maxOcrWidth, maxOcrHeight);
                    graphics.Dispose();
                    graphics = Graphics.FromImage(collage);
                    graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, collage.Width, collage.Height));

                    currentCollageEntryX = collageEntrySeparation;
                    currentCollageEntryY = collageEntrySeparation;
                }

                // Update the entry with the appropriate collage info
                entry.collageCoords = new Rectangle(currentCollageEntryX - collageEntrySeparation,
                    currentCollageEntryY - collageEntrySeparation,
                    collageEntryWidth + 2 * collageEntrySeparation,
                    collageEntryHeight + 2 * collageEntrySeparation);

                entry.collage = collage;

                // Draw the first convincer
                graphics.DrawImage(convincer,
                    new Rectangle(currentCollageEntryX,
                        currentCollageEntryY + (collageEntryHeight - convincer.Height) / 2,
                        convincer.Width,
                        convincer.Height),
                    convincerRectangle,
                    GraphicsUnit.Pixel);

                // Draw the homework box contents, trimming the specified amount
                Rectangle? writingBounds = GetWritingBounds(homework,
                    new Rectangle(entry.mainInfo.homeworkCoords.X + homeworkBoxRemoval,
                        entry.mainInfo.homeworkCoords.Y + homeworkBoxRemoval,
                        entry.mainInfo.homeworkCoords.Width - 2 * homeworkBoxRemoval,
                        entry.mainInfo.homeworkCoords.Height - 2 * homeworkBoxRemoval));

                if (writingBounds == null)
                {
                    // TODO: HANDLE THIS!
                    //       Maybe use an exception instead of a nullable Rectangle?
                    writingBounds = new Rectangle(0, 0, 0, 0);
                }

                //Console.WriteLine(writingBounds.Value.X);
                //Console.WriteLine(writingBounds.Value.Y);
                //Console.WriteLine(writingBounds.Value.Width);
                //Console.WriteLine(writingBounds.Value.Height);

                graphics.DrawImage(homework,
                    new Rectangle(currentCollageEntryX + convincer.Width + convincerSeparation,
                        currentCollageEntryY + (collageEntryHeight - writingBounds.Value.Height) / 2,
                        writingBounds.Value.Width,
                        writingBounds.Value.Height),
                    new Rectangle(writingBounds.Value.X,
                        writingBounds.Value.Y,
                        writingBounds.Value.Width,
                        writingBounds.Value.Height),
                    GraphicsUnit.Pixel);

                // Draw the second convincer
                graphics.DrawImage(convincer,
                    new Rectangle(currentCollageEntryX + convincer.Width + convincerSeparation + writingBounds.Value.Width + convincerSeparation,
                        currentCollageEntryY + (collageEntryHeight - convincer.Height) / 2,
                        convincer.Width,
                        convincer.Height),
                    convincerRectangle,
                    GraphicsUnit.Pixel);

                // Move on to the next horizontal position
                currentCollageEntryX += collageEntryWidth + collageEntrySeparation;
            }

            //TODO: graphics.Dispose() again here?

            collages.Add(collage);
            //}

            return collages;
        }

        private Rectangle? GetWritingBounds(Bitmap homework, Rectangle homeworkBox)
        {
            int startX = GetWritingBoundsStartX(homework, homeworkBox);
            int endX = GetWritingBoundsEndX(homework, homeworkBox);
            int startY = GetWritingBoundsStartY(homework, homeworkBox);
            int endY = GetWritingBoundsEndY(homework, homeworkBox);

            if (startX > homeworkBox.X + homeworkBox.Width - 1
                || endX < homeworkBox.X
                || startY > homeworkBox.Y + homeworkBox.Height - 1
                || endY < homeworkBox.Y)
            {
                return null;
            }

            // TODO: Do I need to add or subtract 1 from width/height?
            return new Rectangle(startX, startY, endX - startX + 1, endY - startY + 1);
        }

        private int GetWritingBoundsStartX(Bitmap homework, Rectangle homeworkBox)
        {
            int x;
            for (x = homeworkBox.X; x <= homeworkBox.X + homeworkBox.Width - 1; x++)
            {
                for (int y = homeworkBox.Y; y <= homeworkBox.Y + homeworkBox.Height - 1; y++)
                {
                    if (homework.GetPixel(x, y).GetBrightness() < writingThreshold)
                    {
                        return x;
                    }
                }
            }

            return x;
        }

        private int GetWritingBoundsEndX(Bitmap homework, Rectangle homeworkBox)
        {
            int x;
            for (x = homeworkBox.X + homeworkBox.Width - 1; x >= homeworkBox.X; x--)
            {
                for (int y = homeworkBox.Y; y <= homeworkBox.Y + homeworkBox.Height - 1; y++)
                {
                    if (homework.GetPixel(x, y).GetBrightness() < writingThreshold)
                    {
                        return x;
                    }
                }
            }

            return x;
        }

        private int GetWritingBoundsStartY(Bitmap homework, Rectangle homeworkBox)
        {
            int y;
            for (y = homeworkBox.Y; y <= homeworkBox.Y + homeworkBox.Height - 1; y++)
            {
                for (int x = homeworkBox.X; x <= homeworkBox.X + homeworkBox.Width - 1; x++)
                {
                    if (homework.GetPixel(x, y).GetBrightness() < writingThreshold)
                    {
                        return y;
                    }
                }
            }

            return y;
        }

        private int GetWritingBoundsEndY(Bitmap homework, Rectangle homeworkBox)
        {
            int y;
            for (y = homeworkBox.Y + homeworkBox.Height - 1; y >= homeworkBox.Y; y--)
            {
                for (int x = homeworkBox.X; x <= homeworkBox.X + homeworkBox.Width - 1; x++)
                {
                    if (homework.GetPixel(x, y).GetBrightness() < writingThreshold)
                    {
                        return y;
                    }
                }
            }

            return y;
        }

        public static void CopyRegionIntoImage(Bitmap srcBitmap, Rectangle srcRegion, ref Bitmap destBitmap, Rectangle destRegion)
        {
            using (Graphics grD = Graphics.FromImage(destBitmap))
            {
                grD.DrawImage(srcBitmap, destRegion, srcRegion, GraphicsUnit.Pixel);
            }
        }

        public static Bitmap DrawFilledRectangle(int x, int y)
        {
            Bitmap bmp = new Bitmap(x, y);
            using (Graphics graph = Graphics.FromImage(bmp))
            {
                Rectangle ImageSize = new Rectangle(0, 0, x, y);
                graph.FillRectangle(Brushes.White, ImageSize);
            }
            return bmp;
        }

        private int GetMaxHomeworkBoxWidth()
        {
            int result = 0;

            foreach (Rectangle r in homeworkBoxes)
            {
                if (r.Width > result)
                {
                    result = r.Width;
                }
            }

            return result;
        }

        private int GetMaxHomeworkBoxHeight()
        {
            int result = 0;

            foreach (Rectangle r in homeworkBoxes)
            {
                if (r.Height > result)
                {
                    result = r.Height;
                }
            }

            return result;
        }

        // To be used for keeping track of collage info and anything else necessary
        private class EntryInfoInternal
        {
            public EntryInfo mainInfo = new EntryInfo();
            public Rectangle collageCoords;
            public Bitmap collage;
        }

        // To be outputted as a final result.
        public class EntryInfo
        {
            public Rectangle homeworkCoords;
            public string ocrResult = "";
        }
    }
}
