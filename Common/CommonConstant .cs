using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.common
{
    public static class CommonConstant
    {
        public static string CONNECTIONSTRING_TEXT = "connectionString";
        public static string SENDGRID_CONNECTIONSTRING_TEXT = "SendgridConnectionString";

        /* ============== DATABASE ============== */

        // PROD
        public static string KEYVALUE_TXT = "prd-saleonmobile-condb";
        // UAT
        //public static string KEYVALUE_TXT = "uat-dbcon-saleonmob";
        // DEV 
        //public static string KEYVALUE_TXT = "dev-uat-database-saleonmob";

        /* ==============DATABASE ============== */


        /* ==============SENDGRID ============== */
        // DEV & UAT
        //public static string KEYVALUE_SENDGRID_TXT = "dev-uat-sendgrid-saleonmob";
        // PROD
        public static string KEYVALUE_SENDGRID_TXT = "prd-saleonmobile-sendgrid";

        /* ==============SENDGRID ============== */


        public static string GetLoggerString = "FunctionAppLogs";
        public static string VERSION = "1.0.0";
    }
}
