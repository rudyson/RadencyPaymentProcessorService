using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Input
{
    internal class SourceRecord
    {
        public string First_Name { get; set; }
        public string Last_Name { get; set; }
        public string Address { get; set; }
        public decimal Payment { get; set; }
        public DateTime Date { get; set; }
        public long Account_Number { get; set; }
        public string Service { get; set;}

    }
}
