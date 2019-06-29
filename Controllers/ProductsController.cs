using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using TodoApi.Models;
using TodoApi.Repository;

namespace TodoApi.Controllers
{
	public class WorkerHostedService : BackgroundService
	{
		protected override async Task ExecuteAsync(CancellationToken stopToken)
		{
			Console.WriteLine("ExecuteAsync start");
			
			await ProductsController.ReplicateAgents();
			
		  int i = 0;
		  while (!stopToken.IsCancellationRequested)
		  {
			if (i++ >= 60)
				i = 0;
			
			try
			{
				if (i == 0)
					await ProductsController.ReplicateAgents();
			}
			catch (Exception)
			{

			}
			
			Thread.Sleep(1000);    
		  }
		  
			Console.WriteLine("ExecuteAsync stop");
		}
	}
	
    [Route("WebApp/api/[controller]")]
    public class ProductsController : Controller
    {
        //static Dictionary<int, Product> products = new Dictionary<int, Product>();
        static public bool doPolling = false;

		static public void ReadIds()
		{
			new ProductsRepo().Load();
		}

		public ProductsController()
		{
		}
		
        // GET api/values
        [HttpGet]
        public IActionResult Get()
        {
            //filter and sort args
			bool active = false;
            if (Request != null && Request.Query.ContainsKey("active"))
            {
                active = Request.Query["active"].ToString() == "y";
            }
            
			bool sort = false;
            if (Request != null && Request.Query.ContainsKey("sbh"))
            {
                sort = Request.Query["sbh"].ToString() == "true";
            }

			List<Product> newProds = new ProductsRepo().GetAll(active ? 120 : -1);
			if (sort) 
				newProds.Sort();
			
			return Ok(newProds);

        }

        // GET api/values/5
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            if (id == 0)
			{
				int freespace  = 0;
				int freespace2 = 0;
				DriveInfo[] allDrives = DriveInfo.GetDrives();
				foreach (DriveInfo d in allDrives)
				{
					//System.Console.WriteLine(d.Name + "=" + d.AvailableFreeSpace);
					if (d.Name == "/")
						freespace = (int)(d.AvailableFreeSpace / (1024*1024));
					string drive = Startup.Configuration["altDriveForSize"];
					if (!string.IsNullOrEmpty(drive) && d.Name == drive)
						freespace2 = (int)(d.AvailableFreeSpace / (1024*1024));
				}

				string username = "";
				if (HttpContext != null && HttpContext.Request != null)
					username = getUsername(HttpContext.Request.Headers["Authorization"]);
				bool isAdmin = username == Startup.Configuration["adminUser"];
				string release = "Unknown";
				if (System.IO.File.Exists(@"/etc/os-release"))
				{
					release = System.IO.File.ReadAllText(@"/etc/os-release");
					int index1 = release.IndexOf("\nID=");
					if (index1 != -1)
						release = release.Substring(index1 + 4);
					index1 = release.IndexOf("\n");
					if (index1 != -1)
						release = release.Substring(0, index1);
				}
                return Ok(new Product() { Id = 0, Name=username, Status = isAdmin.ToString(), OS = release, Version = Startup.Configuration["version"], Percent = freespace, Age = freespace2, IP = "" });
			}
			
            Console.WriteLine(string.Format("Get({0})",id));
			Product prod = new ProductsRepo().GetOne(id);
			
			if (prod == null)
				return NotFound(id);
			
			return Ok(prod);
        }

        public static async Task<string> ReplicateAgents()
        {
			//Console.WriteLine("ReplicateAgents");
			
			if (Startup.Configuration == null)
				return "";
			
            var r = await DownloadPage("http://" + Startup.Configuration["pollingIP"] + "/WebApp/api/products?itemPerPage=999");
            if (string.IsNullOrEmpty(r))
               return "";

            List<Product> p = JsonConvert.DeserializeObject<List<Product>>(r);
            //Console.WriteLine(p.Count);
            //lock (products)
            {
				new ProductsRepo().SetAll(p);
                //Console.WriteLine(DateTime.Now.ToString() + " "  + products.Count);
            }
			
			return "";            
        }
        
        static async Task<string> DownloadPage(string url)
        {
			HttpClientHandler handler = new HttpClientHandler();
			try
			{
				handler.Credentials = new System.Net.NetworkCredential(Startup.Configuration["pollingUser"],
					Startup.Configuration["pollingPass"]);
				
				using(var client = new HttpClient(handler))
				{
					using(var r = await client.GetAsync(new Uri(url)))
					{
						string result = await r.Content.ReadAsStringAsync();
						return result;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("exception: " + ex.Message);
				return "";
			}
        }
                
        static public string getUsername(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 7)
                return "";
            byte[] buffer = Convert.FromBase64String(s.Substring(6));
            string user = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
            int index = user.IndexOf(":");
            if (index != -1)
                return user.Substring(0, index);
            return user;
        }
        
        // POST api/values
        [HttpPost]
        public IActionResult Post(Product p)
        {
            int id = p.Id;
            //Console.WriteLine(string.Format("Post({0})",p.Id));
            //lock (products)
            {
                string username = "";
                if (HttpContext != null && HttpContext.Request != null)
                    username = getUsername(HttpContext.Request.Headers["Authorization"]);
                if (true)
                {
                    bool canAdd = !doPolling && username == Startup.Configuration["adminUser"];
                    //actual post
                    if (!canAdd)
                    {
                        if (doPolling)
                            return Ok(new Product());
                        return Unauthorized();
                    }
                    
					//System.Console.WriteLine(p.Name);
					if (string.IsNullOrEmpty(p.Name))
						return StatusCode(500);
					
					Product np = new Product(){Name = p.Name};
					new ProductsRepo().AddOne(-1, np);
                    return Ok(np);
                }

                // if (!products.ContainsKey(id) || username == "insecure")
                    // return Ok(new Product(){Id = -1});
                // products[id].Percent = p.Percent;
                // products[id].IP = p.IP;
                // products[id].IP2 = HttpContext.Connection.RemoteIpAddress.ToString();
                // products[id].LastReport = DateTime.Now;
                // products[id].Version = p.Version;
                // products[id].OS = p.OS;
                // products[id].Profile = p.Profile;

                // if (!products.ContainsKey(0))
		        // products.Add(0, new Product(){Id=0});
		        // products[0].LastReport = DateTime.Now;
                // return Ok(products[id]);	
            }
        }

        private void writeIds()
        {
			new ProductsRepo().Save();
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody]Product p)
        {
            bool update = false;
            if (Request != null && Request.Query.ContainsKey("update"))
            {
                update = Request.Query["update"].ToString() == "true";
            }

            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = getUsername(HttpContext.Request.Headers["Authorization"]);
            if (username == Startup.Configuration["readonlyUser"] && 
            (string.IsNullOrEmpty(p.Command) || p.Command != "getProperties")
            )
                return Unauthorized();
            
            if (!string.IsNullOrEmpty(p.Command))
                System.Console.WriteLine(p.Command);

			ProductsRepo repo = new ProductsRepo();
			
			if (repo.GetOne(id) == null)
			{
				return NotFound(p);
			}
			
			string ret = "good";
			repo.SetOne(id, p, HttpContext.Connection.RemoteIpAddress.ToString());
			
			Product product = repo.GetOne(id);
			//TODO: use set method instead of updating product
			
			if (update)
			{
					bool doSave = p.Version != product.Version;
					product.Percent = p.Percent;
					product.IP = p.IP;
					product.Version = p.Version;
					product.OS = p.OS;
					product.Profile = p.Profile;
					if (doSave)
						repo.Save();
			}
			else if (!string.IsNullOrEmpty(p.Command))
			{
				//increment the cmd number
				if (string.IsNullOrEmpty(product.Command) || !product.Command.Contains(":"))
				{
					product.Command = "1:" + p.Command;
				}
				else
				{
					int i = 1 + System.Convert.ToInt32(product.Command.Substring(0, product.Command.IndexOf(':')));
					product.Command = i.ToString() + ":" + p.Command;
				}
				ret = product.Command;
				//don't update lastReport time if received command from UI
				return Ok(product);	
			}
			else if (!string.IsNullOrEmpty(p.Response))
			{
				product.Response = p.Response;
			}
			else if (!string.IsNullOrEmpty(p.Output))
			{
				product.setOutput(p.Output);
			}
            else if (!string.IsNullOrEmpty(p.Desc))
            {
                product.Desc = p.Desc;
				repo.Save();
            }
			else if (!string.IsNullOrEmpty(p.Name))
			{
				if (username != Startup.Configuration["adminUser"])
					return Unauthorized();
				product.Name = p.Name;
				writeIds();
			}
			else
			{
				product.Percent = p.Percent;
			}
			
			product.IP2 = HttpContext.Connection.RemoteIpAddress.ToString();
			product.LastReport = DateTime.Now;

			string ip = GetHeaderValueAs<string>("X-Forwarded-For");
			//Console.WriteLine("forwarded from "+ip);
			if (!string.IsNullOrEmpty(ip))
				product.IP2 = ip;
				

			repo.AddOne(0, new Product(){Id=0});
			Product product0 = repo.GetOne(0);
			if (product0 != null)
            {
				product0.LastReport = DateTime.Now;
            }
			
			//must return existing product NOT incoming product, so that we're returning latest command
			return Ok(product);	
        }

		public T GetHeaderValueAs<T>(string headerName)
		{
			StringValues values;

			if (HttpContext?.Request?.Headers?.TryGetValue(headerName, out values) ?? false)
			{
				string rawValues = values.ToString();   // writes out as Csv when there are multiple.

				if (!string.IsNullOrEmpty(rawValues))
					return (T)Convert.ChangeType(values.ToString(), typeof(T));
			}
			return default(T);
		}

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
			new ProductsRepo().DeleteOne(id);
			return Ok(id);
        }
    }
}
