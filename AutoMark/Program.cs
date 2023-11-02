using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;

using System;

using System.Collections.Generic;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace AutoMark
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // TODO: Migrate this all to Alignment.cs
            var model = new Mat(@"boundaries2.png");
            var scene = new Mat(@"scan.png");
            Mat result = new Mat();
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;
            var matches = new VectorOfVectorOfDMatch();
            Mat mask;
            Mat homography;
            Alignment.FindMatch(model, scene, out modelKeyPoints, out observedKeyPoints, matches, out mask, out homography);
            CvInvoke.WarpPerspective(scene, result, homography, model.Size, Inter.Linear, Warp.InverseMap);



            // Load config

            // TODO: Allow other types of questions eventually
            List<QuestionAnswer> questions = new List<QuestionAnswer>();

            using (TextFieldParser parser = new TextFieldParser("coords.csv"))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                // Header
                parser.ReadFields();

                while (!parser.EndOfData)
                {
                    //Process row
                    string[] fields = parser.ReadFields();

                    Rectangle box = new Rectangle(Convert.ToInt32(fields[1]), Convert.ToInt32(fields[2]), Convert.ToInt32(fields[3]), Convert.ToInt32(fields[4]));
                    string questionType = fields[5];

                    if (questionType == "Answer")
                    {
                        // Check if we already have a question with this specific question number
                        QuestionAnswer question = null;
                        foreach (QuestionAnswer test in questions)
                        {
                            if (test.QuestionNumber == fields[0])
                            {
                                question = test;
                                break;
                            }
                        }

                        if (question == null)
                        {
                            question = new QuestionAnswer(fields[0]);
                            questions.Add(question);
                        }

                        question.Boxes.Add(new QuestionAnswer.AnswerBox(box, fields[6], fields[7]));
                    }
                }
            }

            //CvInvoke.Imwrite("realworld_warp_2.png", result);

            // OCR.ReadText("writing.png");

            // Rectangle entry1 = new Rectangle(595, 166, 103, 70);
            // Rectangle entry2 = new Rectangle(595, 241, 103, 90);
            // Rectangle entry3 = new Rectangle(595, 336, 103, 76);



            Ocr ocr = new Ocr();
            ocr.homework = result.ToBitmap();

            //ocr.homeworkBoxes.Add(entry1);
            //ocr.homeworkBoxes.Add(entry2);
            //ocr.homeworkBoxes.Add(entry3);

            foreach (QuestionAnswer question in questions)
            {
                foreach (QuestionAnswer.AnswerBox answerBox in question.Boxes)
                {
                    ocr.homeworkBoxes.Add(answerBox.Box);
                }
            }

            ocr.maxOcrWidth = 1000;
            ocr.maxOcrHeight = 500;

            List<Ocr.EntryInfo> readResults = await ocr.RunOcr();

            // Process the read results.
            foreach (Ocr.EntryInfo readResult in readResults)
            {
                // Find the answer box this corresponds to
                QuestionAnswer.AnswerBox answerBox = null;

                foreach (QuestionAnswer question in questions)
                {
                    bool found = false;

                    foreach (QuestionAnswer.AnswerBox test in question.Boxes)
                    {
                        if (test.Box == readResult.homeworkCoords)
                        {
                            found = true;
                            answerBox = test;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }

                // If we actually found the corresponding answer box
                if (answerBox != null)
                {
                    Console.WriteLine("Box at coordinates: " + "(" + answerBox.Box.X + "," + answerBox.Box.Y + ")"
                        + " -> " + "(" + (answerBox.Box.X + answerBox.Box.Width) + "," + (answerBox.Box.Y + answerBox.Box.Height) + ")");
                    Console.WriteLine("Expected answer: " + answerBox.CorrectAnswer);
                    Console.WriteLine("Actual answer: " + readResult.ocrResult);
                    Console.WriteLine("Is this correct? " + (answerBox.IsCorrect(readResult.ocrResult) ? "Yes" : "No"));
                    Console.WriteLine();
                }
            }
        }
    }
}