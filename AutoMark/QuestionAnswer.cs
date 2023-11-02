using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace AutoMark
{
    class QuestionAnswer
    {
        public List<AnswerBox> Boxes { get; }
        public string QuestionNumber { get; } // String because we could have something like "1.(a)", etc...

        public QuestionAnswer(string questionNumber)
        {
            QuestionNumber = questionNumber;
            Boxes = new List<AnswerBox>();
        }

        public class AnswerBox
        {
            public Rectangle Box { get; }
            public string Type { get; }
            public string CorrectAnswer { get; }

            private float floatTolerance = 0.01F;

            public AnswerBox(Rectangle box, string type, string correctAnswer)
            {
                Box = box;
                Type = type;
                CorrectAnswer = correctAnswer;
            }

            public bool IsCorrect(string studentAnswer)
            {
                // For extra safety, we apply Trim(), even though the answer should be already trimmed?
                if (studentAnswer.Trim() == "")
                {
                    return false;
                }

                try
                {
                    switch (Type)
                    {
                        case "int":
                            return Convert.ToInt32(studentAnswer) == Convert.ToInt32(CorrectAnswer);

                        case "float":
                            return (Math.Abs(Convert.ToDouble(studentAnswer) - Convert.ToDouble(CorrectAnswer)) < floatTolerance);
                    }
                }
                catch (System.FormatException)
                {
                    Console.WriteLine("BAD ANSWER FORMAT");
                }

                return false;
            }
        }
    }
}
