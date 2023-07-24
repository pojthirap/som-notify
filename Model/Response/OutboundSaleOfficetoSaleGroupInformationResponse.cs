﻿using AzureFunctionAppNotify.Model.Response.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureFunctionAppNotify.Model.Response.OutboundSaleOfficetoSaleGroupInformationResponse
{
    public class OutboundSaleOfficetoSaleGroupInformationResponse : ResponseBase<Data>
    {
        public List<Data> Data { get; set; }
    }


    public class Data
    {
        public long Update_Date { get; set; }
        public string Update_Time { get; set; }
        public string Status_IND { get; set; }
        public string Sales_Office { get; set; }
        public string Sales_Office_Name { get; set; }
        public string Sales_Group { get; set; }
        public string Sales_Group_Name { get; set; }
    }
}
