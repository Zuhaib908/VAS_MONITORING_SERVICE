using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VAS_Service.DAL
{
    internal class Response
    {
        public int Statuscode { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}
