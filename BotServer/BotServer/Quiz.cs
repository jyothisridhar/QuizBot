using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

namespace BotServer
{
    [DataContract]
    class Question
    {
        [DataMember]
        public string question;
        [DataMember]
        public string[] options;
        [DataMember]
        public string answer;
    }

    class Quiz
    {
        internal static bool _newQuiz = false;
        internal static int _question_no = 0;
        internal static int _score = 0;

        internal static List<Question> _qList = new List<Question>();

        //load json data into list
        public static void loadJson(string filePath)
        {
            string topic = "../../quizDB/" + filePath;
            _newQuiz = false;

            using (StreamReader r = new StreamReader(topic))
            {
                string source = r.ReadToEnd();
                DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<Question>));
                MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
                _qList = (List<Question>)js.ReadObject(memStream);
            }
        }

        //extract question from list
        public static string GetQuestion(int qIndex)
        {
            if (_qList.Count > qIndex)
            {
                string questionString = _qList[qIndex].question;
                string options = "a. " + " " + _qList[qIndex].options[0] + 
                    "b. " + " " + _qList[qIndex].options[1] +
                    "c. " + " " + _qList[qIndex].options[2] + 
                    "d. " + " " + _qList[qIndex].options[3];
                return "Question: \n" + questionString + "Options:" +
                    options;
            }
            else
            {
                _question_no = 0;
                _newQuiz = false;
                return "End of Quiz! Generating report..";
            }
        }

        internal static string ShowResult()
        {
            return _score.ToString();
        }

        internal static string ValidateAnswer(string userChoice)
        {
            //todo: set timer
            //todo: record answer of user and store in DB?

            if (userChoice == _qList[_question_no].answer)
            {
                _score++;
                return " Great going!";
            }
            else
                return "  Sorry wrong answer";
        }
    }
}
