﻿using AzureFunctionAppNotify.Model.Request.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.Model.Request.OutboundSaleOrderItemTypeRequest
{
    public class OutboundSaleOrderItemTypeRequest
    {
        public RequestInput Input { get; set; }

    }
    public class RequestInput : RequestBase
    {
        public string All_data { get; set; }
    }
}
