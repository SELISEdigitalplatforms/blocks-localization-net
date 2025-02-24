using System.Net;

namespace DomainService.Services
{
    public class AiCompletionModel
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public double temperature { get; set; }

        public AiCompletionModel ConstructCommand(string content, double temperature)
        {
            return new AiCompletionModel
            {
                model = "gpt-3.5-turbo",
                messages = new List<Message>
                {
                    new Message
                    {
                        role = "user",
                        content = content
                    }
                },
                temperature = temperature
            };
        }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class RestResponse
    {
        public HttpStatusCode HttpStatusCode { set; get; }
        public dynamic ResponseData { get; set; }
    }
}
