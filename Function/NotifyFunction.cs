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
    public  class NotifyFunction
    {
        private readonly IConfiguration _configuration;
        private string connectionString_ = null;
        //private readonly ISendGridClient sendGridClient;
        private ILogger logger;

        public NotifyFunction(IConfiguration configuration)
        {
            _configuration = configuration;
            string keyValue = _configuration[CommonConstant.KEYVALUE_TXT];
            string connectionString = _configuration[CommonConstant.CONNECTIONSTRING_TEXT];
            connectionString_ = String.IsNullOrEmpty(keyValue) ? connectionString : keyValue;
            Console.WriteLine("keyValue:" + keyValue);
            Console.WriteLine("connectionString:" + connectionString_);

        }

        [Function("NotifyFunction")]
        public  async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(CommonConstant.GetLoggerString);
            this.logger = logger;
            logger.LogInformation("C# HTTP trigger function processed a request. NotifyFunction");

            var responses = req.CreateResponse(HttpStatusCode.OK);
            responses.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            responses.WriteString("connectionString : " + connectionString_);

            // SQL Scope
            string WAITING_FOR_APPROVE = PlanTripStatus.WAITING_FOR_APPROVE.ToString("d");
            StringBuilder queryBuilder = new StringBuilder();
            queryBuilder.AppendFormat(" select T.PLAN_TRIP_ID,T.PLAN_TRIP_NAME,T.CREATE_USER,E.FIRST_NAME+' '+E.LAST_NAME SENDER_NAME ");
            queryBuilder.AppendFormat(" ,CFG1.PARAM_VALUE SENDER_EMAIL,AE.EMAIL RECEIVE_EMAIL ");
            queryBuilder.AppendFormat(" from PLAN_TRIP T ");
            queryBuilder.AppendFormat(" inner join ADM_EMPLOYEE E on E.EMP_ID = T.CREATE_USER ");
            queryBuilder.AppendFormat(" inner join ADM_EMPLOYEE AE on AE.EMP_ID = E.APPROVE_EMP_ID ");
            queryBuilder.AppendFormat(" inner join MS_CONFIG_PARAM CFG1 on CFG1.PARAM_KEYWORD = 'EMAIL_SENDER' ");
            queryBuilder.AppendFormat(" inner join MS_CONFIG_PARAM CFG2 on CFG2.PARAM_KEYWORD = 'NOTIFY_PLANTRIP_WAITING_FOR_APPROVE' ");
            queryBuilder.AppendFormat(" where T.STATUS = '{0}' ", WAITING_FOR_APPROVE);
            queryBuilder.AppendFormat(" and DATEDIFF(DD,T.PLAN_TRIP_DATE,dbo.GET_SYSDATETIME()) = CFG2.PARAM_VALUE ");
            string queryString = queryBuilder.ToString();

            logger.LogInformation("Query:" + queryBuilder.ToString());
            Console.WriteLine("Query:" + queryBuilder.ToString());
            List<EmailForSend> emailForSendList = new List<EmailForSend>();
            using (SqlConnection connection = new SqlConnection(connectionString_))
            {
                SqlCommand command = new SqlCommand(queryString, connection);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    EmailForSend data;
                    while (reader.Read())
                    {
                        data = new EmailForSend();
                        IDataRecord record = (IDataRecord)reader;
                        data.PlanTripId = QueryUtils.getValueAsString(record, "PLAN_TRIP_ID");
                        data.PlanTripName = QueryUtils.getValueAsString(record, "PLAN_TRIP_NAME");
                        data.CreateUser = QueryUtils.getValueAsString(record, "CREATE_USER");
                        data.SenderName = QueryUtils.getValueAsString(record, "SENDER_NAME");
                        data.SenderEmail = QueryUtils.getValueAsString(record, "SENDER_EMAIL");
                        data.ReceiveEmail = QueryUtils.getValueAsString(record, "RECEIVE_EMAIL");
                        emailForSendList.Add(data);
                    }
                    // Call Close when done reading.
                    reader.Close();
                }
            }



            string EMP_ID, Subject, Body, SenderEmail, ReceiveEmail, SenderName;
            string EmailTemplate = EmailTemplateStatus.WAITING_FOR_APPROVE_ALERT.ToString("d");

            if(emailForSendList.Count != 0)
            {
                foreach(EmailForSend e in emailForSendList)
                {
                    SenderName = e.SenderName;
                    EMP_ID = e.CreateUser;
                    Subject = "Plan Trip (Alert) : Waiting for approve";
                    Body = "Waiting for approve : มี Plan Trip Name [Name : " + e.PlanTripName + "] จาก [Employee Name : " + SenderName + "] ส่งถึงท่านรอการอนุมัติ";
                    SenderEmail = e.SenderEmail;
                    ReceiveEmail = e.ReceiveEmail;

                    // Set Object For Send Email
                    SendMailModel sendMailModel = new SendMailModel();
                    sendMailModel.SenderName = SenderName;
                    sendMailModel.MailTo = new List<string> { ReceiveEmail };
                    sendMailModel.showAllRecipients = false;
                    sendMailModel.Subject = Subject;
                    sendMailModel.Content = Body;
                    //


                    string OBJ_EMAIL = JsonConvert.SerializeObject(sendMailModel);// Convert Object For Send Email -> JsonString
                    decimal TABLE_REF_KEY_ID = Convert.ToDecimal(e.PlanTripId);
                    EmailJobModel emailJobModel = new EmailJobModel();
                    emailJobModel.EmailTemplate = EmailTemplate;
                    emailJobModel.ObjEmail = OBJ_EMAIL;
                    emailJobModel.TableRefKeyId = TABLE_REF_KEY_ID;
                    emailJobModel.EmpId = EMP_ID;
                    try
                    {
                        EmailJob emailJob = await Add(emailJobModel);
                    }catch(Exception ex)
                    {

                    }
                }
            }


            responses = req.CreateResponse(HttpStatusCode.OK);
            return responses;
        }


        protected async Task<EmailJob> Add(EmailJobModel emailJobModel)
        {

            // SQL Scope
            string VAL_EMAIL_JOB_SEQ = "";
            using (SqlConnection connection = new SqlConnection(connectionString_))
            {
                connection.Open();
                // Create SQL
                String sql = "select NEXT VALUE FOR EMAIL_JOB_SEQ as SEQ_";
                SqlCommand command = connection.CreateCommand();
                // Execute SQL
                logger.LogDebug("Query:" + sql);
                Console.WriteLine("Query:" + sql);
                command.CommandText = sql;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        VAL_EMAIL_JOB_SEQ = reader.GetValue(reader.GetOrdinal("SEQ_")).ToString();
                    }
                }


                command = connection.CreateCommand();
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
                    bulder.AppendFormat(" INSERT INTO EMAIL_JOB ([JOB_ID], [EMAIL_TEMPLATE_ID], [TABLE_REF_KEY_ID], [OBJ_EMAIL], [JOB_STATUS], [JOB_STATUS_FAIL_COUNT], [CREATE_USER], [CREATE_DTM], [UPDATE_USER], [UPDATE_DTM])  ");
                    bulder.AppendFormat(" VALUES(@VAL_EMAIL_JOB_SEQ, @EmailTemplate, @TABLE_REF_KEY_ID, @OBJ_EMAIL, 'W', 0, @EMP_ID, dbo.GET_SYSDATETIME(), @EMP_ID, dbo.GET_SYSDATETIME()) ");
                    command.Parameters.Add("@VAL_EMAIL_JOB_SEQ", SqlDbType.Decimal);
                    command.Parameters["@VAL_EMAIL_JOB_SEQ"].Value = VAL_EMAIL_JOB_SEQ;
                    command.Parameters.Add("@EmailTemplate", SqlDbType.Decimal);
                    command.Parameters["@EmailTemplate"].Value = emailJobModel.EmailTemplate;
                    command.Parameters.Add("@TABLE_REF_KEY_ID", SqlDbType.NVarChar);
                    command.Parameters["@TABLE_REF_KEY_ID"].Value = emailJobModel.TableRefKeyId;
                    command.Parameters.Add("@OBJ_EMAIL", SqlDbType.NVarChar);
                    command.Parameters["@OBJ_EMAIL"].Value = emailJobModel.ObjEmail;
                    command.Parameters.Add("@EMP_ID", SqlDbType.NVarChar);
                    command.Parameters["@EMP_ID"].Value = emailJobModel.EmpId;

                    // Execute SQL
                    logger.LogDebug("Query:" + bulder.ToString());
                    Console.WriteLine("Query:" + bulder.ToString());
                    command.CommandText = bulder.ToString();
                    Int32 rowsAffected = command.ExecuteNonQuery();
                    logger.LogDebug("RowsAffected:" + rowsAffected);
                    Console.WriteLine("RowsAffected:" + rowsAffected);

                    // Attempt to commit the transaction.
                    transaction.Commit();
                    logger.LogDebug("Commit to database.");
                    Console.WriteLine("Commit to database.");
                    EmailJob re = new EmailJob();
                    re.JobId = VAL_EMAIL_JOB_SEQ;
                    return re;
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

        }

        /*


        protected async Task<SendMailCustom> SendEmail(string language, SendMailModel sendMailModel, string mailSender, ISendGridClient sendGridClient, ILogger<NotifyFunction> logger)
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
                string CalendarContent = NotifyFunction.MeetingRequestString(sendMailModel.Meeting.Organizer, sendMailModel.MailTo, sendMailModel.Meeting.Subject, sendMailModel.Meeting.Description, sendMailModel.Meeting.Location, DateTimeStart, DateTimeEnd);

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


        private string getMailCalendarFormat(string dateTime, ILogger<NotifyFunction> logger)
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
        */

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
            public string PlanTripId { get; set; }
            public string PlanTripName { get; set; }
            public string CreateUser { get; set; }
            public string SenderName { get; set; }
            public string SenderEmail { get; set; }
            public string ReceiveEmail { get; set; }
        }

        public class EmailJobModel
        {


            public string EmailTemplate { get; set; }
            public decimal TableRefKeyId { get; set; }
            public string ObjEmail { get; set; }
            public string EmpId { get; set; }


        }

        public class EmailJob
        {
            public string JobId { get; set; }
        }

    }
}
