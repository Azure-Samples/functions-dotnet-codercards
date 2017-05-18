using System;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using Microsoft.Azure.WebJobs.Host;

using static CoderCardsLibrary.ImageHelpers;
using Microsoft.Azure.WebJobs;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace CoderCardsLibrary
{
    [StorageAccount("AzureWebJobsStorage")]
    public class CardGenerator
    {
        [FunctionName("GenerateCard")]
        public static async Task GenerateCard(
            [QueueTrigger("%input-queue%")] CardInfoMessage cardInfo,
            [Blob("%input-container%/{BlobName}", FileAccess.Read)] byte[] image, 
            [Blob("%output-container%/{BlobName}", FileAccess.Write)] Stream outputBlob, TraceWriter log)
        {
            Emotion[] faceDataArray = await RecognizeEmotionAsync(image, log);

            if (faceDataArray == null) { 
                log.Error("No result from Emotion API");
                return;
            }

            if (faceDataArray.Length == 0) {
                log.Error("No face detected in image");
                return;
            }

            var faceData = faceDataArray[0]; 
            var card = GetCardImageAndScores(faceDataArray[0].Scores, out double score); // assume exactly one face

            MergeCardImage(card, image, cardInfo.PersonName, cardInfo.Title, score);

            SaveAsJpeg(card, outputBlob);
        }

        [FunctionName("RequestImageProcessing")]
        [return: Queue("%input-queue%")]
        public static CardInfoMessage RequestImageProcessing([HttpTrigger(AuthorizationLevel.Anonymous, new string[] { "POST" })] CardInfoMessage input, TraceWriter log)
        {
            return input;
        }

        [FunctionName("Settings")]
        public static SettingsMessage Settings([HttpTrigger(AuthorizationLevel.Anonymous, new string[] { "GET" })] string input, TraceWriter log)
        {
            string stage = (Environment.GetEnvironmentVariable("STAGE") == null) ? "LOCAL" : Environment.GetEnvironmentVariable("STAGE");
            return new SettingsMessage() {
                Stage = stage,
                SiteURL = ((stage == "LOCAL") ? "http://": "https://") + Environment.GetEnvironmentVariable("SITEURL"),
                StorageURL = Environment.GetEnvironmentVariable("STORAGE_URL"),
                ContainerSAS = Environment.GetEnvironmentVariable("CONTAINER_SAS"),
                InputContainerName = Environment.GetEnvironmentVariable("input-container"),
                OutputContainerName = Environment.GetEnvironmentVariable("output-container")
            };
        }

        static Image GetCardImageAndScores(EmotionScores scores, out double score)
        {
            NormalizeScores(scores);

            var cardBack = "neutral.png";
            score = scores.Neutral;
            const int angerBoost = 2, happyBoost = 4;

            if (scores.Surprise > 10) {
                cardBack = "surprised.png";
                score = scores.Surprise;
            }
            else if (scores.Anger > 10) {
                cardBack = "angry.png";
                score = scores.Anger * angerBoost;
            }
            else if (scores.Happiness > 50) {
                cardBack = "happy.png";
                score = scores.Happiness * happyBoost;
            }

            return Image.FromFile(GetFullImagePath(cardBack));
        }

        #region Helpers

        public class CardInfoMessage
        {
            public string PersonName { get; set; }
            public string Title { get; set; }
            public string BlobName { get; set; }
        }

        static async Task<Emotion[]> RecognizeEmotionAsync(byte[] image, TraceWriter log)
        {
            try
            {
                var emotionServiceClient = new EmotionServiceClient(Environment.GetEnvironmentVariable(EmotionAPIKeyName));

                using (MemoryStream faceImageStream = new MemoryStream(image))
                {
                    return await emotionServiceClient.RecognizeAsync(faceImageStream);
                }

            }
            catch (Exception e)
            {
                log.Error("Error processing image", e);
                return null;
            }

        }

        private const string EmotionAPIKeyName = "EmotionAPIKey";
        private const string AssetsFolderLocation = "assets";

        static string GetFullImagePath(string filename)
        {
            var path = Path.Combine(
                Environment.GetEnvironmentVariable("HOME"), 
                Environment.GetEnvironmentVariable("SITE_PATH"), 
                AssetsFolderLocation,
                filename);

            return Path.GetFullPath(path);
        }

        public class SettingsMessage
        {
            public string Stage { get; set; }
            public string SiteURL { get; set; }
            public string StorageURL { get; set; }
            public string ContainerSAS { get; set; }
            public string InputContainerName { get; set; }
            public string OutputContainerName { get; set; }
        }

        #endregion
    }
}
