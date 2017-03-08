using Blueloan.Web.Controllers;
using Blueloan.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using TypeLite;

namespace TypeGap
{
    class Program
    {
        static void Main(string[] args)
        {
            new TypeFluent()
                .AddWebApi<AccountController>()
                .Add<Loan>()
                .Add<Borrower>()
                .Add<LoanSecurity>()
                .Build(@"C:\Users\csayler\Source\Repos\blueloan\src\Blueloan.UI\src");
        }
    }
}
