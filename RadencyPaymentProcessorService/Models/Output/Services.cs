using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Output
{
    internal class Services
    {
        public string Name { get; set; }
        public List<Payer> Payers { get; set; }
        public decimal Total
        {
            get
            {
                return this.Payers.Select(x => x.Payment).Sum();
            }
        }
    }
}
