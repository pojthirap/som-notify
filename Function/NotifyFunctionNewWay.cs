using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AzureFunctionAppNotify.common;
using AzureFunctionAppNotify.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AzureFunctionAppNotify
{
    public  class NotifyFunctionNewWay
    {
        /*
        private readonly IConfiguration _configuration;
        private string connectionString_ = null;
        //private readonly ISendGridClient sendGridClient;
        //private readonly ILogger<NotifyFunction> logger;

        public NotifyFunction(IConfiguration configuration)
        {
            _configuration = configuration;
            string keyValue = _configuration[CommonConstant.KEYVALUE_TXT];
            string connectionString = _configuration[CommonConstant.CONNECTIONSTRING_TEXT];
            connectionString_ = String.IsNullOrEmpty(keyValue) ? connectionString : keyValue;
            Console.WriteLine("keyValue:" + keyValue);
            Console.WriteLine("connectionString:" + connectionString_);

            string SendgridConnectionString = _configuration[CommonConstant.SENDGRID_TEXT];
            Console.WriteLine("SendgridConnectionString:" + SendgridConnectionString);

            //this.sendGridClient = sendGridClient;
            //this.logger = logger;

        }

        [FunctionName("NotifyFunction")]
        //[Function("NotifyFunction")]
        public static async Task Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext executionContext,
 [SendGrid(ApiKey = "SendgridConnectionString")] IAsyncCollector<SendGridMessage> messageCollector)
        {
            //var emailObject = JsonSerializer.Deserialize<OutgoingEmail>(Encoding.UTF8.GetString(email.Body));
            OutgoingEmail emailObject = new OutgoingEmail();6
            emailObject.To = "beeharu123456789@gmail.com";
            emailObject.From = "No-reply@pt.co.th";
            emailObject.Subject = "Subject";
            emailObject.Body = "<h1>My First Heading</h1< p > My first paragraph.</ p > ";

            var message = new SendGridMessage();
            message.AddTo(emailObject.To);
            message.AddContent("text/html", emailObject.Body);
            message.SetFrom(new EmailAddress(emailObject.From));
            message.SetSubject(emailObject.Subject);

            await messageCollector.AddAsync(message);
        }

        public class OutgoingEmail
        {
            public string To { get; set; }
            public string From { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
        }
        */
    }
}
