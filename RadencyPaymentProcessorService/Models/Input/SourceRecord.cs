using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Input
{
    internal class SourceRecord
    {
        public string First_name { get; set; }
        public string Last_name { get; set; }
        public string Address { get; set; }
        public decimal Payment { get; set; }
        public DateTime Date { get; set; }
        public long Account_number { get; set; }
        public string Service { get; set;}
        public override string ToString()
        {
            return $"{First_name},{Last_name},\"{Address}\",{Payment},{Date},{Account_number},{Service}";
        }

    }
}
