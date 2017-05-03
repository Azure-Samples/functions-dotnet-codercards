﻿using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Drawing;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;

using static CoderCardsLibrary.ImageHelpers;
using Microsoft.Azure.WebJobs;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Common.Contract;

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
        public static void RequestImageProcessing(
            [HttpTrigger] CardInfoMessage input, 
            [Queue("%input-queue%")] out CardInfoMessage cardInfo,
            TraceWriter log)
        {
            cardInfo = input;
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

        #endregion
    }
}
