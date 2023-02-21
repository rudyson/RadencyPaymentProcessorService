using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Output
{
    internal class Payer
    {
        
        public string Name { get; set; }
        public decimal Payment { get; set; }
        public DateTime Date { get; set; }
        public long Account_Number { get; set; }
    }
}
