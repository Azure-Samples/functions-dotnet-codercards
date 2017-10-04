using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;
using Microsoft.ProjectOxford.Common.Contract;
using System.IO;

namespace CoderCardsLibrary
{
    public class ImageHelpersXPlat
    {
        #region Pixel locations
        const int TopLeftFaceX = 85;
        const int TopLeftFaceY = 187;
        const int FaceWidth = 648; // Width = Height
        const int NameTextX = 56;
        const int NameTextY = 88;
        const int TitleTextY = 125;
        const int NameWidth = 430;
        const int ScoreX = 640;
        const int ScoreY = 110;
        const int ScoreWidth = 117;

        const int CardWidth = 819;
        const int CardHeight = 1150;
        #endregion

        #region Font info
        const string FontFamilyName = "Microsoft Sans Serif";
        const int NameFontSize = 38;
        const int TitleFontSize = 30;
        const short ScoreFontSize = 55;
        #endregion

        public static void MergeCardImage(string cardPath, byte[] imageBytes, Stream outputStream, string personName, string personTitle, double score)
        {
            using (MemoryStream faceImageStream = new MemoryStream(imageBytes)) {
                using (var surface = SKSurface.Create(width: CardWidth, height: CardHeight, colorType: SKImageInfo.PlatformColorType, alphaType: SKAlphaType.Premul)) {
                    SKCanvas canvas = surface.Canvas;

                    canvas.DrawColor(SKColors.White); // clear the canvas / fill with white

                    using (var fileStream = File.OpenRead(cardPath)) 
                    using (var stream = new SKManagedStream(fileStream))  // decode the bitmap from the stream
                    using (var cardBack = SKBitmap.Decode(stream)) 
                    using (var face = SKBitmap.Decode(imageBytes))
                    using (var paint = new SKPaint()) {
                        canvas.DrawBitmap(cardBack, SKRect.Create(0, 0, CardWidth, CardHeight), paint);
                        canvas.DrawBitmap(face, SKRect.Create(TopLeftFaceX, TopLeftFaceY, FaceWidth, FaceWidth));

                        RenderText(canvas, NameFontSize, NameTextX, NameTextY, NameWidth, personName);
                        RenderText(canvas, TitleFontSize, NameTextX, TitleTextY, NameWidth, personTitle);
                        RenderScore(canvas, ScoreX, ScoreY, ScoreWidth, score.ToString());

                        canvas.Flush();

                        using (var jpgImage = surface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 80)) {
                            jpgImage.SaveTo(outputStream);
                        }
                    }                   
                }
            }
        }


        public static void RenderScore(SKCanvas canvas, int xPos, int yPos, int width, string score)
        {
            var font = SKTypeface.FromFamilyName(FontFamilyName, SKTypefaceStyle.Bold);
            var brush = CreateBrush(font, ScoreFontSize);

            var textWidth = brush.MeasureText(score);

            canvas.DrawText(score, xPos + width - textWidth, yPos, brush);
        }


        public static void RenderText(SKCanvas canvas, int fontSize, int xPos, int yPos, int width, string text)
        {
            var font = SKTypeface.FromFamilyName(FontFamilyName, SKTypefaceStyle.Bold);

            SKPaint brush = null;
            float textWidth;

            do {
                brush = CreateBrush(font, fontSize);
                textWidth = brush.MeasureText(text);
                fontSize--;
            }
            while (textWidth > width);

            canvas.DrawText(text, xPos, yPos, brush);
        }

        static SKPaint CreateBrush(SKTypeface font, int fontSize)
        {
            return new SKPaint {
                Typeface = font,
                TextSize = fontSize,
                IsAntialias = true,
                Color = new SKColor(0, 0, 0)
            };
        }

        public static float RoundScore(float score) => (float)Math.Round((decimal)(score * 100), 0);

        public static void NormalizeScores(EmotionScores scores)
        {
            scores.Anger = RoundScore(scores.Anger);
            scores.Happiness = RoundScore(scores.Happiness);
            scores.Neutral = RoundScore(scores.Neutral);
            scores.Sadness = RoundScore(scores.Sadness);
            scores.Surprise = RoundScore(scores.Surprise);
        }
    }
}
