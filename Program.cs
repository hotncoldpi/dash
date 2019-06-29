using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace TodoApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
			bool doPolling = true;
	        if (args.Length > 0)
            {
                if (args[0] != "kestrel")
		            doPolling = true;
                else if (args.Length > 1)
		            doPolling = true;
                else
		            doPolling = false;
            }
            else
            {
		        doPolling = false;
            }
            
            //init static
            Controllers.ProductsController.ReadIds();
			
			if (doPolling) {
				Controllers.ProductsController.doPolling = true;
				//new Controllers.ServerPoller();
			}
			
            var host = new WebHostBuilder()
                //.UseUrls("http://0.0.0.0:5000")
               	.UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
