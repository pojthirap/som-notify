using AzureFunctionAppNotify.Model.Request.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.Model.Request.OutboundCustomerInformationRequest
{
    public class OutboundCustomerInformationRequest
    {
        public RequestInput Input { get; set; }
        public List<DivisionInput> Division { get; set; }

    }
    public class RequestInput : RequestBase
    {
        public long Start_Date { get; set; }
        public long Start_Time { get; set; }
        public long End_Date { get; set; }
        public long End_Time { get; set; }
    }

    public class DivisionInput
    {
        public string Division { get; set; }
    }
}
