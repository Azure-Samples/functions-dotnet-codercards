using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;

using static CoderCardsLibrary.ImageHelpers;
using System.Net.Http.Headers;
using System.Net;

namespace CoderCardsLibrary
{
    public class CardGenerator
    {
        private const string EMOTION_API_URI      = "https://api.projectoxford.ai/emotion/v1.0/recognize";
        private const string EMOTION_API_KEY_NAME = "EmotionAPIKey";
        private const string ASSETS_FOLDER        = "assets";

        public static async Task Run(byte[] image, string filename, Stream outputBlob, TraceWriter log)
        {
            string result = await CallEmotionAPI(image);
            log.Info(result);

            if (String.IsNullOrEmpty(result)) {
                log.Error("No result from Emotion API");
                return;
            }

            var imageData = JsonConvert.DeserializeObject<Face[]>(result);

            if (imageData.Length == 0) {
                log.Error("No face detected in image");
                return;
            }

            double score = 0;
            var faceData = imageData[0]; // assume exactly one face
            var card = GetCardImageAndScores(faceData.Scores, out score);

            var personInfo = GetNameAndTitle(filename); // extract name and title from filename
            MergeCardImage(card, image, personInfo, score);

            SaveAsJpeg(card, outputBlob);
        }

        public static Tuple<string, string> GetNameAndTitle(string filename)
        {
            string[] words = filename.Split('-');

            return words.Length > 1 ? Tuple.Create(words[0], words[1]) : Tuple.Create("", "");
        }

        static Image GetCardImageAndScores(Scores scores, out double score)
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

        static async Task<string> CallEmotionAPI(byte[] image)
        {
            var client = new HttpClient();

            var content = new StreamContent(new MemoryStream(image));
            var key = Environment.GetEnvironmentVariable(EMOTION_API_KEY_NAME);

            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var httpResponse = await client.PostAsync(EMOTION_API_URI, content);

            if (httpResponse.StatusCode == HttpStatusCode.OK) {
                return await httpResponse.Content.ReadAsStringAsync();
            }

            return null;
        }

        static string GetFullImagePath(string filename)
        {
            var appPath = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "site", "wwwroot", ASSETS_FOLDER);
            return Path.Combine(appPath, filename);
        }
    }
}
