using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RadencyPaymentProcessorService.Models.Output
{
    internal class CityModel
    {
        public string City { get; set; }
        public List<Service> Services { get; set; }
        public decimal Total
        {
            get
            {
                return this.Services.Select(x => x.Total).Sum();
            }
        }
    }
}
