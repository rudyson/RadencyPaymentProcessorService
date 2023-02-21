using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Output
{
    internal class Cities
    {
        public string City { get; set; }
        public List<Services> Services { get; set; }
        public decimal Total
        {
            get
            {
                return this.Services.Select(x => x.Total).Sum();
            }
        }
    }
}
