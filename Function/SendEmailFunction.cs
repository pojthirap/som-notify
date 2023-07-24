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
using AzureFunctionAppNotify.Common;
using AzureFunctionAppNotify.EnumVal;
using AzureFunctionAppNotify.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AzureFunctionAppNotify
{
    public  class SendEmailFunction
    {
        private readonly IConfiguration _configuration;
        private string connectionString_ = null;
        private readonly ISendGridClient sendGridClient;
        //private readonly ILogger<NotifyFunction> logger;
        private ILogger logger;

        public SendEmailFunction(IConfiguration configuration)
        {
            _configuration = configuration;
            string keyValue = _configuration[CommonConstant.KEYVALUE_TXT];
            string connectionString = _configuration[CommonConstant.CONNECTIONSTRING_TEXT];
            connectionString_ = String.IsNullOrEmpty(keyValue) ? connectionString : keyValue;
            Console.WriteLine("keyValue:" + keyValue);
            Console.WriteLine("connectionString:" + connectionString_);

            string SendgridKeyValue = _configuration[CommonConstant.KEYVALUE_SENDGRID_TXT];
            string SendgridConnectionString = _configuration[CommonConstant.SENDGRID_CONNECTIONSTRING_TEXT];
            SendgridConnectionString = String.IsNullOrEmpty(SendgridKeyValue) ? SendgridConnectionString : SendgridKeyValue;
            Console.WriteLine("SendgridKeyValue:" + SendgridKeyValue);
            Console.WriteLine("SendgridConnectionString:" + SendgridConnectionString);

            this.sendGridClient = new SendGridClient(SendgridConnectionString);
            

        }

        [Function("SendEmailFunction")]
        public  async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(CommonConstant.GetLoggerString);
            this.logger = logger;
            logger.LogInformation("C# HTTP trigger function processed a request. SendEmailFunction");

            var responses = req.CreateResponse(HttpStatusCode.OK);
            responses.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            responses.WriteString("connectionString : " + connectionString_);

            // Get Config From Database
            DbConfig dbConfig = await CommonConfigDB.getInterfaceSapConfigAsync(connectionString_, logger);
            string SenderEmail = dbConfig.EmailSender;
            // SQL Scope
            using (SqlConnection connection = new SqlConnection(connectionString_))
            {
                connection.Open();
                SqlCommand command = connection.CreateCommand();
                SqlTransaction transaction;
                // Start a local transaction.
                transaction = connection.BeginTransaction("T1");
                // Must assign both transaction object and connection
                // to Command object for a pending local transaction
                command.Connection = connection;
                command.Transaction = transaction;

                try
                {
                    // INSERT SYNC_DATA_LOG
                    // Create SQL
                    command.Parameters.Clear();
                    StringBuilder bulder = new StringBuilder();

                    bulder.AppendFormat("  UPDATE J ");
                    bulder.AppendFormat(" SET  ");
                    bulder.AppendFormat("     J.JOB_STATUS='O', UPDATE_USER ='NOTIFY_SYSTEM', UPDATE_DTM=dbo.GET_SYSDATETIME() ");
                    bulder.AppendFormat(" FROM EMAIL_JOB J ");
                    bulder.AppendFormat(" INNER JOIN MS_CONFIG_PARAM CFG on CFG.PARAM_KEYWORD = 'NOTIFY_EMAIL_JOB_STATUS_FAIL_LIMIT' ");
                    bulder.AppendFormat(" where CFG.PARAM_VALUE = J.JOB_STATUS_FAIL_COUNT ");

                    // Execute SQL
                    logger.LogInformation("Query:" + bulder.ToString());
                    Console.WriteLine("Query:" + bulder.ToString());
                    command.CommandText = bulder.ToString();
                    Int32 rowsAffected = command.ExecuteNonQuery();
                    logger.LogInformation("RowsAffected:" + rowsAffected);
                    Console.WriteLine("RowsAffected:" + rowsAffected);


                    // SQL Scope
                    bulder = new StringBuilder();
                    bulder.AppendFormat(" select * ");
                    bulder.AppendFormat("     from EMAIL_JOB J ");
                    bulder.AppendFormat("     where J.JOB_STATUS IN('W','F') ");
                    logger.LogInformation("Query:" + bulder.ToString());
                    Console.WriteLine("Query:" + bulder.ToString());
                    List<EmailForSend> emailForSendList = new List<EmailForSend>();
                    command.Parameters.Clear();
                    command.CommandText = bulder.ToString();
                    using (SqlDataReader reader = command.ExecuteReader())
                        {
                            EmailForSend data;
                            while (reader.Read())
                            {
                                data = new EmailForSend();
                                IDataRecord record = (IDataRecord)reader;
                                data.JobId = QueryUtils.getValueAsString(record, "JOB_ID");
                                data.EmailTemplateId = QueryUtils.getValueAsString(record, "EMAIL_TEMPLATE_ID");
                                data.TableRefKeyId = QueryUtils.getValueAsString(record, "TABLE_REF_KEY_ID");
                                data.ObjEmail = QueryUtils.getValueAsString(record, "OBJ_EMAIL");
                                data.JobStatus = QueryUtils.getValueAsString(record, "JOB_STATUS");
                                data.ErrorDesc = QueryUtils.getValueAsString(record, "ERROR_DESC");
                                data.CreateUser = QueryUtils.getValueAsString(record, "CREATE_USER");
                                data.CreateDtm = QueryUtils.getValueAsString(record, "CREATE_DTM");
                                data.UpdateUser = QueryUtils.getValueAsString(record, "UPDATE_USER");
                                data.UpdateDtm = QueryUtils.getValueAsString(record, "UPDATE_DTM");
                                data.JobStatusFailCount = QueryUtils.getValueAsString(record, "JOB_STATUS_FAIL_COUNT");
                                emailForSendList.Add(data);
                            }
                            // Call Close when done reading.
                            reader.Close();
                        }

                    if (emailForSendList.Count != 0)
                    {
                        foreach (EmailForSend e in emailForSendList)
                        {

                            string JOB_ID = e.JobId;
                            string JOB_STATUS = "S";
                            int JOB_STATUS_FAIL_COUNT = String.IsNullOrEmpty(e.JobStatusFailCount) ? 0 : int.Parse(e.JobStatusFailCount);
                            string ERROR_DESC = "";


                            // Set Object For Send Email
                            SendMailModel sendMailModel = JsonConvert.DeserializeObject<SendMailModel>(e.ObjEmail);
                            try
                            {
                                SendMailCustom sendMailCustom = await SendEmail("th", sendMailModel, SenderEmail, sendGridClient, this.logger);
                            }catch(Exception ex)
                            {
                                ERROR_DESC = ex.Message + ":" + ex.ToString();
                                JOB_STATUS = "F";
                                JOB_STATUS_FAIL_COUNT++;
                            }


                            // Create SQL
                            command.Parameters.Clear();
                            bulder = new StringBuilder();

                            bulder.AppendFormat(" UPDATE EMAIL_JOB  ");
                            bulder.AppendFormat(" SET [JOB_STATUS]=@JOB_STATUS, [JOB_STATUS_FAIL_COUNT]=@JOB_STATUS_FAIL_COUNT, [ERROR_DESC]=@ERROR_DESC, [UPDATE_USER]='NOTIFY_SYSTEM', [UPDATE_DTM]=dbo.GET_SYSDATETIME() ");
                            bulder.AppendFormat(" WHERE [JOB_ID]= @JOB_ID  ");
                            command.Parameters.Add("@JOB_STATUS", SqlDbType.NVarChar);
                            command.Parameters["@JOB_STATUS"].Value = JOB_STATUS;
                            command.Parameters.Add("@JOB_STATUS_FAIL_COUNT", SqlDbType.Decimal);
                            command.Parameters["@JOB_STATUS_FAIL_COUNT"].Value = JOB_STATUS_FAIL_COUNT;
                            command.Parameters.Add("@ERROR_DESC", SqlDbType.NVarChar);
                            command.Parameters["@ERROR_DESC"].Value = ERROR_DESC;
                            command.Parameters.Add("@JOB_ID", SqlDbType.Decimal);
                            command.Parameters["@JOB_ID"].Value = JOB_ID;
                            // Execute SQL
                            logger.LogInformation("Query:" + bulder.ToString());
                            Console.WriteLine("Query:" + bulder.ToString());
                            command.CommandText = bulder.ToString();
                            rowsAffected = command.ExecuteNonQuery();
                            logger.LogInformation("RowsAffected:" + rowsAffected);
                            Console.WriteLine("RowsAffected:" + rowsAffected);
                            //

                        }
                    }



                    // Attempt to commit the transaction.
                    transaction.Commit();
                    logger.LogInformation("Commit to database.");
                    Console.WriteLine("Commit to database.");
                }
                catch (Exception ex)
                {
                    logger.LogInformation("Commit Exception Type: {0}", ex.GetType());
                    logger.LogInformation("  Message: {0} {1}", ex.Message, ex.ToString());
                    Console.WriteLine("Commit Exception Type: {0}", ex.GetType());
                    Console.WriteLine("  Message: {0} {1}", ex.Message, ex.ToString());
                    try
                    {
                        transaction.Rollback();
                        throw;
                    }
                    catch (Exception ex2)
                    {
                        throw;
                    }
                }
            }
            // SQL Scope


            
/*
            string SenderEmail = "No-reply@pt.co.th";
            // Set Object For Send Email
            SendMailModel sendMailModel = new SendMailModel();
            sendMailModel.SenderName = "SenderName";
            sendMailModel.MailTo = new List<string> { "beeharu123456789@gmail.com" };
            sendMailModel.showAllRecipients = false;
            sendMailModel.Subject = "Subject";
            sendMailModel.Content = "Body";
            //

            SendMailCustom sendMailCustom = await SendEmail("th", sendMailModel, SenderEmail, sendGridClient, this.logger);
            */
            responses = req.CreateResponse(HttpStatusCode.OK);
            return responses;
        }





        protected async Task<SendMailCustom> SendEmail(string language, SendMailModel sendMailModel, string mailSender, ISendGridClient sendGridClient, ILogger logger)
        {

            var from = new EmailAddress(mailSender, sendMailModel.SenderName);
            List<EmailAddress> to = new List<EmailAddress>();
            foreach (string e in sendMailModel.MailTo)
            {
                to.Add(new EmailAddress(e));
            }
            var subject = sendMailModel.Subject;
            var plainTextContent = "";// "Sending calendar and easy to do anywhere, even with C#";
                                      //var htmlContent = "<strong>"+ sendMailModel.Content+ "</strong>";
            var htmlContent = sendMailModel.Content;
            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(from, to, subject, plainTextContent, htmlContent, sendMailModel.showAllRecipients);
            if (sendMailModel.Meeting != null)
            {
                string startDateTime = getMailCalendarFormat(sendMailModel.Meeting.StartDateTime, logger);
                startDateTime += " 00:01";
                string endDateTime = getMailCalendarFormat(sendMailModel.Meeting.EndDateTime, logger);
                endDateTime += " 23:59";
                logger.LogDebug("startDateTime=" + startDateTime);
                logger.LogDebug("endDateTime=" + endDateTime);
                Console.WriteLine("startDateTime=" + startDateTime);
                Console.WriteLine("endDateTime=" + endDateTime);
                //CultureInfo culture = new CultureInfo("en-US");
                CultureInfo culture = new CultureInfo("th-TH");
                DateTime DateTimeStart = Convert.ToDateTime(startDateTime, culture);
                DateTime DateTimeEnd = Convert.ToDateTime(endDateTime, culture);
                string CalendarContent = SendEmailFunction.MeetingRequestString(sendMailModel.Meeting.Organizer, sendMailModel.MailTo, sendMailModel.Meeting.Subject, sendMailModel.Meeting.Description, sendMailModel.Meeting.Location, DateTimeStart, DateTimeEnd);

                byte[] calendarBytes = Encoding.UTF8.GetBytes(CalendarContent.ToString());
                SendGrid.Helpers.Mail.Attachment calendarAttachment = new SendGrid.Helpers.Mail.Attachment();
                calendarAttachment.Filename = "invite.ics";
                //the Base64 encoded content of the attachment.
                calendarAttachment.Content = Convert.ToBase64String(calendarBytes);
                calendarAttachment.Type = "text/calendar";
                msg.Attachments = new List<SendGrid.Helpers.Mail.Attachment>() { calendarAttachment };
            }
            if (sendMailModel.MailCC != null && sendMailModel.MailCC.Count != 0)
            {
                List<EmailAddress> cc = new List<EmailAddress>();
                foreach (string e in sendMailModel.MailCC)
                {
                    cc.Add(new EmailAddress(e));
                }
                msg.AddCcs(cc);
            }
            if (sendMailModel.MailBCC != null && sendMailModel.MailBCC.Count != 0)
            {
                List<EmailAddress> bcc = new List<EmailAddress>();
                foreach (string e in sendMailModel.MailBCC)
                {
                    bcc.Add(new EmailAddress(e));
                }
                msg.AddBccs(bcc);
            }
            var response = await sendGridClient.SendEmailAsync(msg);
            string logStr = ("response code:" + response.StatusCode
                + ",IsSuccessStatusCode:" + response.IsSuccessStatusCode
                + ",Headers:" + response.Headers
                + ",Body:" + response.Body
                );
            logger.LogDebug(logStr);
            logger.LogDebug("End Test SendGrid");
            Console.WriteLine(logStr);
            Console.WriteLine("End Test SendGrid");

            SendMailCustom model = new SendMailCustom();
            model.StatusCode = response.StatusCode;
            model.IsSuccessStatusCode = response.IsSuccessStatusCode;
            //onBeforeSendResponse("SendMail", "SendMail", model);
            return model;


        }


        public static string MeetingRequestString(string from, List<string> toUsers, string subject, string desc, string location, DateTime startTime, DateTime endTime, int? eventID = null, bool isCancel = false)
        {
            StringBuilder str = new StringBuilder();

            str.AppendLine("BEGIN:VCALENDAR");
            str.AppendLine("PRODID:-//Microsoft Corporation//Outlook 12.0 MIMEDIR//EN");
            str.AppendLine("VERSION:2.0");
            str.AppendLine(string.Format("METHOD:{0}", (isCancel ? "CANCEL" : "REQUEST")));
            str.AppendLine("BEGIN:VEVENT");

            str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmssZ}", startTime.ToUniversalTime()));
            str.AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmss}", DateTime.Now));
            str.AppendLine(string.Format("DTEND:{0:yyyyMMddTHHmmssZ}", endTime.ToUniversalTime()));
            str.AppendLine(string.Format("LOCATION: {0}", location));
            str.AppendLine(string.Format("UID:{0}", (eventID.HasValue ? "blablabla" + eventID : Guid.NewGuid().ToString())));
            str.AppendLine(string.Format("DESCRIPTION:{0}", desc.Replace("\n", "<br>")));
            str.AppendLine(string.Format("X-ALT-DESC;FMTTYPE=text/html:{0}", desc.Replace("\n", "<br>")));
            str.AppendLine(string.Format("SUMMARY:{0}", subject));

            str.AppendLine(string.Format("ORGANIZER;CN=\"{0}\":MAILTO:{1}", from, from));
            str.AppendLine(string.Format("ATTENDEE;CN=\"{0}\";RSVP=TRUE:mailto:{1}", string.Join(",", toUsers), string.Join(",", toUsers)));

            str.AppendLine("BEGIN:VALARM");
            str.AppendLine("TRIGGER:-PT15M");
            str.AppendLine("ACTION:DISPLAY");
            str.AppendLine("DESCRIPTION:Reminder");
            str.AppendLine("END:VALARM");
            str.AppendLine("END:VEVENT");
            str.AppendLine("END:VCALENDAR");

            return str.ToString();
        }


        private string getMailCalendarFormat(string dateTime, ILogger logger)
        {
            if (String.IsNullOrEmpty(dateTime))
            {
                return dateTime;
            }


            Console.WriteLine("getMailCalendarFormat startDateTime=" + dateTime);
            Console.WriteLine("getMailCalendarFormat endDateTime=" + dateTime);
            logger.LogInformation("getMailCalendarFormat startDateTime=" + dateTime);
            logger.LogInformation("getMailCalendarFormat endDateTime=" + dateTime);

            if (dateTime.Contains("T"))
            {
                string tmp = dateTime.Split("T")[0];
                string[] tmpArr = tmp.Split("-");
                tmp = tmpArr[2] + "/" + tmpArr[1] + "/" + (Int32.Parse(tmpArr[0]) + 543);
                return tmp;
            }
            else
            {
                string tmp = dateTime.Split(" ")[0];
                string[] tmpArr = tmp.Split("/");
                tmp = tmpArr[1] + "/" + tmpArr[0] + "/" + (Int32.Parse(tmpArr[2]) + 543);
                return tmp;
            }

        }


        public class SendMailCustom
        {

            public HttpStatusCode StatusCode { get; set; }
            public bool IsSuccessStatusCode { get; set; }

        }

        public class SendMailModel
        {
            public string SenderName { get; set; }
            public List<string> MailTo { get; set; }
            public bool showAllRecipients { get; set; }
            public List<string> MailCC { get; set; }
            public List<string> MailBCC { get; set; }
            public string Subject { get; set; }
            public string Content { get; set; }
            public MeetingModel Meeting { get; set; }


        }

        public class MeetingModel
        {
            public string Organizer { get; set; }
            public string Subject { get; set; }
            public string Description { get; set; }
            public string Location { get; set; }
            public string StartDateTime { get; set; }
            public string EndDateTime { get; set; }
        }

        public class EmailForSend
        {

            public string JobId { get; set; }
            public string EmailTemplateId { get; set; }
            public string TableRefKeyId { get; set; }
            public string ObjEmail { get; set; }
            public string JobStatus { get; set; }
            public string ErrorDesc { get; set; }
            public string CreateUser { get; set; }
            public string CreateDtm { get; set; }
            public string UpdateUser { get; set; }
            public string UpdateDtm { get; set; }
            public string JobStatusFailCount { get; set; }

        }

    }
}
