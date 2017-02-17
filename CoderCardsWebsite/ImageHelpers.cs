using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace CoderCardsLibrary
{
    #region POCO definitions 
    public class Face
    {
        public FaceRectangle FaceRectangle { get; set; }
        public Scores Scores { get; set; }
    }

    public class FaceRectangle
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class Scores
    {
        public double Anger { get; set; }
        public double Contempt { get; set; }
        public double Disgust { get; set; }
        public double Fear { get; set; }
        public double Happiness { get; set; }
        public double Neutral { get; set; }
        public double Sadness { get; set; }
        public double Surprise { get; set; }
    }
    #endregion

    public class ImageHelpers
    {
        #region Pixel locations
        const int TopLeftFaceX  = 85;
        const int TopLeftFaceY  = 187;
        const int FaceRect      = 648;
        const int NameTextX     = 56;
        const int NameTextY     = 60;
        const int TitleTextX    = 108;
        const int NameWidth     = 430;
        const int ScoreX        = 654;
        const int ScoreY        = 70;
        const int ScoreWidth    = 117;
        #endregion

        #region Font info
        const string FontFamilyName = "Microsoft Sans Serif";
        const int NameFontSize = 38;
        const int TitleFontSize = 30;
        const short ScoreFontSize = 55;
        #endregion

        // This code uses System.Drawing to merge images and render text on the image
        // System.Drawing SHOULD NOT be used in a production application
        // It is not supported in server scenarios and is used here as a demo only!
        public static Image MergeCardImage(Image card, byte[] imageBytes, Tuple<string, string> personInfo, double score)
        {
            using (MemoryStream faceImageStream = new MemoryStream(imageBytes))
            {
                using (Image faceImage = Image.FromStream(faceImageStream, true))
                {
                    using (Graphics g = Graphics.FromImage(card))
                    {
                        g.DrawImage(faceImage, TopLeftFaceX, TopLeftFaceY, FaceRect, FaceRect);
                        RenderText(g, NameFontSize, NameTextX, NameTextY, NameWidth, personInfo.Item1);
                        RenderText(g, TitleFontSize, NameTextX + 4, TitleTextX, NameWidth, personInfo.Item2); // second line seems to need some left padding

                        RenderScore(g, ScoreX, ScoreY, ScoreWidth, score.ToString());
                    }

                    return card;
                }
            }
        }

        public static void RenderScore(Graphics graphics, int xPos, int yPos, int width, string score)
        {
            var brush = new SolidBrush(Color.Black);
            var font = CreateFont(ScoreFontSize);
            SizeF size = graphics.MeasureString(score, font);

            graphics.DrawString(score, font, brush, width - size.Width + xPos, yPos);
        }

        private static Font CreateFont(int fontSize)
        {
            return new Font(FontFamilyName, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        }

        public static void RenderText(Graphics graphics, int fontSize, int xPos, int yPos, int width, string text)
        {
            var brush = new SolidBrush(Color.Black);
            var font = CreateFont(fontSize);
            SizeF size;

            do
            {
                font = CreateFont(fontSize--);
                size = graphics.MeasureString(text, font);
            }
            while (size.Width > width);

            graphics.DrawString(text, font, brush, xPos, yPos);
        }

        // save with higher quality than the default, to avoid jpeg artifacts on the text and numbers
        public static void SaveAsJpeg(Image image, Stream outputStream)
        {
            var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            var qualityEncoder = System.Drawing.Imaging.Encoder.Quality;
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(qualityEncoder, 90L);

            image.Save(outputStream, jpgEncoder, encoderParams);
        }

        static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        public static double RoundScore(double score) => Math.Round(score * 100);

        public static void NormalizeScores(Scores scores)
        {
            scores.Anger     = RoundScore(scores.Anger);
            scores.Happiness = RoundScore(scores.Happiness);
            scores.Neutral   = RoundScore(scores.Neutral);
            scores.Sadness   = RoundScore(scores.Sadness);
            scores.Surprise  = RoundScore(scores.Surprise);
        }
    }
}
