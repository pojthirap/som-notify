﻿using AzureFunctionAppNotify.Model.Response.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.Model.Response.OutboundSaleOrderDocumentTypesResponse
{
    public class OutboundSaleOrderDocumentTypesResponse : ResponseBase<Data>
    {
        public List<Data> Data { get; set; }
    }


    public class Data
    {
        public long Update_Date { get; set; }
        public long Update_Time { get; set; }
        public string Status_IND { get; set; }
        public string Sales_Doc_Type { get; set; }
        public string Desc { get; set; }
    }
}
