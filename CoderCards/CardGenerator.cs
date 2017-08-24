using System;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Common.Contract;
using static CoderCardsLibrary.ImageHelpersXPlat;

namespace CoderCardsLibrary
{
    public class CardGenerator
    {
        [FunctionName("GenerateCard")]
        public static async Task GenerateCard(
            [QueueTrigger("%input-queue%")] CardInfoMessage cardInfo,
            [Blob("%input-container%/{BlobName}", FileAccess.Read)] byte[] image, 
            [Blob("%output-container%/{BlobName}", FileAccess.Write)] Stream outputBlob,
            ExecutionContext context, TraceWriter log)
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
            string cardPath = GetCardImageAndScores(faceDataArray[0].Scores, out double score, context.FunctionDirectory); // assume exactly one face

            log.Info($"CardPath: {cardPath}");

            MergeCardImage(cardPath, image, outputBlob, cardInfo.PersonName, cardInfo.Title, score);
        }

        [FunctionName("RequestImageProcessing")]
        public static string RequestImageProcessing(
            CardInfoMessage input, 
            [Queue("%input-queue%")] out CardInfoMessage queueOutput)
        {
            queueOutput = input;
            return "Ok";
        }

        [FunctionName("Settings")]
        public static SettingsMessage Settings(string input, TraceWriter log)
        {
            string stage = (Environment.GetEnvironmentVariable("STAGE") == null) ? "LOCAL" : Environment.GetEnvironmentVariable("STAGE");
            return new SettingsMessage() {
                Stage = stage,
                SiteURL = Environment.GetEnvironmentVariable("SITEURL"),
                StorageURL = Environment.GetEnvironmentVariable("STORAGE_URL"),
                ContainerSAS = Environment.GetEnvironmentVariable("CONTAINER_SAS"),
                InputContainerName = Environment.GetEnvironmentVariable("input-container"),
                OutputContainerName = Environment.GetEnvironmentVariable("output-container")
            };
        }

        static string GetCardImageAndScores(EmotionScores scores, out double score, string functionDirectory)
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

            var path = Path.Combine(functionDirectory, "../", AssetsFolderLocation, cardBack);
            return Path.GetFullPath(path);
        }

        #region Helpers

        private const string EmotionAPIKeyName = "EmotionAPIKey";
        private const string AssetsFolderLocation = "assets";
        private const string EmotionAPIKey = "28c7e1412c254cb584715bada6706f4d";

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
                var emotionServiceClient = new EmotionServiceClient(EmotionAPIKey);

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
