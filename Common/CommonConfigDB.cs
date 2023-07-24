using AzureFunctionAppNotify.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.Common
{
    public class CommonConfigDB
    {

        private static async Task<string> ReadCommonConfigDB(string connectionString, string ParamKeyword)
        {
            string ParamValue = null;
            StringBuilder queryBuilder = new StringBuilder();
            queryBuilder.AppendFormat(" select * from MS_CONFIG_PARAM where ACTIVE_FLAG = 'Y' ");
            queryBuilder.AppendFormat(" and PARAM_KEYWORD = '{0}' ", ParamKeyword);
            string queryString = queryBuilder.ToString();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(queryString, connection);
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        IDataRecord record = (IDataRecord)reader;
                        ParamValue = QueryUtils.getValueAsString(record, "PARAM_VALUE");
                    }
                    // Call Close when done reading.
                    reader.Close();
                }
            }

            return ParamValue;
        }

        public static async Task<DbConfig> getInterfaceSapConfigAsync(string connectionString, ILogger logger)
        {
            DbConfig emailConfig = new DbConfig();
            emailConfig.EmailSender = await ReadCommonConfigDB(connectionString, "EMAIL_SENDER");

            logger.LogDebug("EMAIL_SENDER:" + emailConfig.EmailSender);
            Console.WriteLine("EMAIL_SENDER:" + emailConfig.EmailSender);
            return emailConfig;
        }


    }

    public class DbConfig
    {
        public string EmailSender { get; set; }
    }
}
