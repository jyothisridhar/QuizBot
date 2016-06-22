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
        public static void loadJson()
        {
            using (StreamReader r = new StreamReader("gk.js"))
            {
                string source = r.ReadToEnd();
                DataContractJsonSerializer js = new DataContractJsonSerializer(typeof(List<Question>));
                MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
                Question q = (Question)js.ReadObject(memStream);
            }
        }
    }
}
