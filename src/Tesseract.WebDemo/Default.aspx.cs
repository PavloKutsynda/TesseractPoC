/*
 * Created by SharpDevelop.
 * User: W110
 * Date: 15/12/2013
 * Time: 8:21 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Xml.Serialization;
using Image = System.Drawing.Image;

namespace Tesseract.WebDemo
{
    // http://stackoverflow.com/questions/9480013/image-processing-to-improve-tesseract-ocr-accuracy https://www.quora.com/How-can-I-improve-the-accuracy-of-Tesseract-OCR
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public class DefaultPage : System.Web.UI.Page
    {
        #region Data

        // input panel controls

        protected Panel inputPanel;
        protected HtmlInputFile imageFile;
        protected HtmlButton submitFile;

        // result panel controls
        protected Panel resultPanel;
        protected HtmlGenericControl meanConfidenceLabel;
        protected HtmlTextArea resultText;
        protected HtmlTextArea ResultTextHtmlTextArea;
        protected HtmlTextArea ResultTextXHtmlTextArea;
        protected HtmlTextArea ResultXml;
        protected HtmlButton restartButton;


        #endregion

        #region Event Handlers

        private void OnSubmitFileClicked(object sender, EventArgs args)
        {
            if (imageFile.PostedFile != null && imageFile.PostedFile.ContentLength > 0)
            {
                // for now just fail hard if there's any error however in a propper app I would expect a full demo.

                using (var engine = new TesseractEngine(Server.MapPath(@"~/tessdata"), "eng", EngineMode.Default))
                {
                    // have to load Pix via a bitmap since Pix doesn't support loading a stream.
                    using (var image1 = new System.Drawing.Bitmap(imageFile.PostedFile.InputStream))
                    {
                        using (var pix = PixConverter.ToPix(image1))
                        {
                            using (var page = engine.Process(pix))
                            {
                                meanConfidenceLabel.InnerText = String.Format("{0:P}", page.GetMeanConfidence());
                                resultText.InnerText = page.GetText();
                                ResultTextHtmlTextArea.InnerText = GetHocrText(page);
                                ResultTextXHtmlTextArea.InnerText = page.GetHOCRText(1);
                                ResultXml.InnerHtml = GetOcrXml(page);

                                //File.WriteAllText("D:\\TesseractTEXT.txt", resultText.InnerText);
                                //File.WriteAllText("D:\\TesseractHTML.txt", ResultTextHtmlTextArea.InnerText);
                                //File.WriteAllText("D:\\TesseractXML.txt", ResultXml.InnerHtml);
                            }
                        }
                    }
                }
                inputPanel.Visible = false;
                resultPanel.Visible = true;
            }
        }

        private string GetHocrText(Page page)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var result = page.GetHOCRText(1, false);
            sw.Stop();
            return result;
        }

        private string GetOcrXml(Page page)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var words = new OrcWors()
            {
                Words = new List<OcrWordEntity>()
            };

            var iterator = page.GetIterator();
            do
            {
                Rect rect;
                if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out rect))
                {
                    words.Words.Add(new OcrWordEntity
                    {
                        Left = rect.X1,
                        Right = rect.X2,
                        Top = rect.Y1,
                        Bottom = rect.Y2,
                        Word = iterator.GetText(PageIteratorLevel.Word)
                    });
                }
            }
            while (iterator.Next(PageIteratorLevel.Word));

            var result = SerializeObject(words);
            sw.Stop();
            return result;
        }

        public string SerializeObject(object obj)
        {
            var serializer = new XmlSerializer(obj.GetType());

            var ns = new XmlSerializerNamespaces();
            ns.Add(string.Empty, string.Empty);
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, obj, ns);

                return writer.ToString();
            }
        }

        private void OnRestartClicked(object sender, EventArgs args)
        {
            resultPanel.Visible = false;
            inputPanel.Visible = true;
        }

        #endregion

        #region Page Setup
        protected override void OnInit(EventArgs e)
        {
            InitializeComponent();
            base.OnInit(e);
        }

        //----------------------------------------------------------------------
        private void InitializeComponent()
        {
            this.restartButton.ServerClick += OnRestartClicked;
            this.submitFile.ServerClick += OnSubmitFileClicked;
        }

        #endregion
    }

    #region XML

    [XmlRoot("OcrWords")]
    public class OrcWors
    {
        [XmlArray("Words")]
        [XmlArrayItem("Word")]
        public List<OcrWordEntity> Words { get; set; }
    }

    public class OcrWordEntity
    {
        [XmlText]
        public string Word { get; set; }

        [XmlAttribute("L")]
        public int Left { get; set; }

        [XmlAttribute("T")]
        public int Top { get; set; }

        [XmlAttribute("R")]
        public int Right { get; set; }

        [XmlAttribute("B")]
        public int Bottom { get; set; }
    }

    public static class ImageHelper
    {
        public static Bitmap CreateNonIndexedImage(Image src)
        {
            Bitmap newBmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics gfx = Graphics.FromImage(newBmp))
            {
                gfx.DrawImage(src, 0, 0);
            }

            return newBmp;
        }

        //Resize
        public static Bitmap Resize(Bitmap bmp, int newWidth, int newHeight)
        {

            Bitmap temp = (Bitmap)bmp;

            Bitmap bmap = new Bitmap(newWidth, newHeight, temp.PixelFormat);

            double nWidthFactor = (double)temp.Width / (double)newWidth;
            double nHeightFactor = (double)temp.Height / (double)newHeight;

            double fx, fy, nx, ny;
            int cx, cy, fr_x, fr_y;
            Color color1 = new Color();
            Color color2 = new Color();
            Color color3 = new Color();
            Color color4 = new Color();
            byte nRed, nGreen, nBlue;

            byte bp1, bp2;

            for (int x = 0; x < bmap.Width; ++x)
            {
                for (int y = 0; y < bmap.Height; ++y)
                {

                    fr_x = (int)Math.Floor(x * nWidthFactor);
                    fr_y = (int)Math.Floor(y * nHeightFactor);
                    cx = fr_x + 1;
                    if (cx >= temp.Width) cx = fr_x;
                    cy = fr_y + 1;
                    if (cy >= temp.Height) cy = fr_y;
                    fx = x * nWidthFactor - fr_x;
                    fy = y * nHeightFactor - fr_y;
                    nx = 1.0 - fx;
                    ny = 1.0 - fy;

                    color1 = temp.GetPixel(fr_x, fr_y);
                    color2 = temp.GetPixel(cx, fr_y);
                    color3 = temp.GetPixel(fr_x, cy);
                    color4 = temp.GetPixel(cx, cy);

                    // Blue
                    bp1 = (byte)(nx * color1.B + fx * color2.B);

                    bp2 = (byte)(nx * color3.B + fx * color4.B);

                    nBlue = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    // Green
                    bp1 = (byte)(nx * color1.G + fx * color2.G);

                    bp2 = (byte)(nx * color3.G + fx * color4.G);

                    nGreen = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    // Red
                    bp1 = (byte)(nx * color1.R + fx * color2.R);

                    bp2 = (byte)(nx * color3.R + fx * color4.R);

                    nRed = (byte)(ny * (double)(bp1) + fy * (double)(bp2));

                    bmap.SetPixel(x, y, System.Drawing.Color.FromArgb
            (255, nRed, nGreen, nBlue));
                }
            }

            return bmap;

        }

        //SetGrayscale
        public static Bitmap SetGrayscale(Bitmap img)
        {

            Bitmap temp = (Bitmap)img;
            Bitmap bmap = (Bitmap)temp.Clone();
            Color c;
            for (int i = 0; i < bmap.Width; i++)
            {
                for (int j = 0; j < bmap.Height; j++)
                {
                    c = bmap.GetPixel(i, j);
                    byte gray = (byte)(.299 * c.R + .587 * c.G + .114 * c.B);

                    bmap.SetPixel(i, j, Color.FromArgb(gray, gray, gray));
                }
            }
            return (Bitmap)bmap.Clone();

        }
        //RemoveNoise
        public static Bitmap RemoveNoise(Bitmap bmap)
        {

            for (var x = 0; x < bmap.Width; x++)
            {
                for (var y = 0; y < bmap.Height; y++)
                {
                    var pixel = bmap.GetPixel(x, y);
                    if (pixel.R < 162 && pixel.G < 162 && pixel.B < 162)
                        bmap.SetPixel(x, y, Color.Black);
                }
            }

            for (var x = 0; x < bmap.Width; x++)
            {
                for (var y = 0; y < bmap.Height; y++)
                {
                    var pixel = bmap.GetPixel(x, y);
                    if (pixel.R > 162 && pixel.G > 162 && pixel.B > 162)
                        bmap.SetPixel(x, y, Color.White);
                }
            }

            return bmap;
        }
    }

    #endregion
}