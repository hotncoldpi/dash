using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
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
		
		class Sorter : IComparer<Product>
		{
			public int Compare(Product x, Product y) {
				if (x.Id < y.Id) return -1; 
				return (x.Id > y.Id) ? 1 : 0; 
			}
		}

        // GET api/values
        [HttpGet]
        public IActionResult Get()
        {
            if (Request != null && Request.Query.ContainsKey("unused"))
            {
                bool unused = Request.Query["unused"].ToString() == "y";
				if (unused)
					return Ok(new ProductsRepo().GetUnused());
            }

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
			
			try
			{
				if (sort) 
					newProds.Sort();
				else
					newProds.Sort(new Sorter());
			}
			catch (Exception ex)
			{
				System.Console.WriteLine(ex.Message);
			}
			
			return Ok(newProds);

        }

        // GET api/values/5
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
			// foreach (var v in HttpContext.Request.Headers)
			// 	System.Console.WriteLine(v.Key);

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
				if (string.IsNullOrEmpty(username))
					username = getUsername2(HttpContext.Request.Headers["Cookie"]);

//foreach (var head in HttpContext.Request.Headers)
//	System.Console.WriteLine(head);

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
                return Ok(new Product() { Id = 0, 
					Name=username, 
					Status = isAdmin.ToString(), 
					OS = release, 
					Version = Startup.Configuration["version"], 
					Percent = freespace, 
					Age = freespace2, 
					IP = doPolling ? "from " + Startup.Configuration["pollingIP"] : "",
					AlertCondition = doPolling });
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
                
		static public string getUsername2(string s)
		{
			int i = s.IndexOf("user=");
			if (i == -1)
				return "";

			s = s.Substring(i+5);
			i = s.IndexOf('&');
			if (i == -1)
				return "";
			s = s.Substring(0, i);
			return s;
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
				if (string.IsNullOrEmpty(username))
					username = getUsername2(HttpContext.Request.Headers["Cookie"]);
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
                    
					//System.Console.WriteLine("id="+p.Id);
					if (string.IsNullOrEmpty(p.Name))
						return StatusCode(500);
					
					Product np = new Product(){Name = p.Name};
					new ProductsRepo().AddOne(p.Id > 0 ? p.Id : -1, np);
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

            bool uploadhist = false;
            if (Request != null && Request.Query.ContainsKey("uploadhist"))
            {
                uploadhist = Request.Query["uploadhist"].ToString() == "true";
            }

            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = getUsername2(HttpContext.Request.Headers["Cookie"]);

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
                	if (string.IsNullOrEmpty(p.Status))
					{
						product.Percent = p.Percent;
						product.IP = p.IP;
					}
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
            else if (uploadhist)
            {
				Console.WriteLine("UploadHistory " + p.UploadHistory);
                product.UploadHistory = p.UploadHistory;
				//repo.Save();
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
				

			repo.AddOne(0, new Product(){Id=0,Name=""});
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

    [Route("WebApp/[controller]")]
    public class LogController : Controller
    {
		void createCSV(string agentid, string dir)
		{
			using (StreamWriter sw = System.IO.File.CreateText("tmp/" + agentid + "/" + dir + "/export.csv"))
			{
				sw.WriteLine("GUID="+dir);
				sw.WriteLine("\"ID\",\"AgentID\",\"Data\",\"DataExt\",\"When\",\"Alert\"");

				foreach (var v in Directory.EnumerateFiles("tmp/" + agentid + "/" + dir))
				
				{
					string filename = v.Substring(1 + v.LastIndexOf('/'));
					int underscore = filename.IndexOf('_');
					if (underscore == -1) 
						continue;
					string fileid = filename.Substring(0, underscore);
					string filenm = filename.Substring(underscore + 1);

					sw.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"",
												fileid,
												agentid,
												filenm,
												"", 
												new DateTime(long.Parse(fileid)).ToString(),
												false));
				}
			}

		}
        [HttpGet("[action]/{agentid}/{id}")]
        public ActionResult Files(string agentid, string id)
        {
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);

			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();

			try
			{
				createCSV(agentid, id);
				System.IO.File.Delete("/tmp/test.zip");
				System.IO.Compression.ZipFile.CreateFromDirectory("./tmp/"+agentid+"/"+id+"/", "/tmp/test.zip",
					System.IO.Compression.CompressionLevel.Optimal, true);

				return new PhysicalFileResult("/tmp/test.zip", "application/zip");
			}
			catch (Exception ex)
			{
				return Ok(ex.Message);
			}

			//return Ok(agentid + " " + id);
		}

        [HttpGet("[action]/{id}")]
        new public ActionResult File(string id, string agentid, string filename)
        {
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);

			if (username == Startup.Configuration["readonlyUser"])
				return Unauthorized();

			try
			{
				//System.Console.WriteLine(id + " " + agentid + " " + filename);
				return new FileStreamResult(new FileStream("tmp/"+agentid+"/"+filename,FileMode.Open), 
					filename.EndsWith(".zip") ? "application/zip" : "image/jpeg");
			}
			catch (Exception)
			{
				return NotFound();
			}
		}
	}
	
    [Route("WebApp/api/[controller]")]
    public class StorageController : Controller
    {
        static Dictionary<int, List<string>> hashes = new Dictionary<int, List<string>>();

		bool checkHash(MemoryStream ms, int intId, string filename)
		{
			bool useDB2 = true;

			//this prevents a duplicate image from being saved
			byte[] bytes = SHA1.Create().ComputeHash(ms.ToArray());
			string hextext = BitConverter.ToString(bytes).Replace("-", "").ToLower();
			if (hashes.ContainsKey(intId))
			{
				string lasthash = "";
				List<string> newhashlist = new List<string>();
				var hashlist = hashes[intId];
				foreach (var hash in hashlist)
					if (hash.Substring(40) == filename)
						lasthash = hash.Substring(0, 40);
					else
						newhashlist.Add(hash);

				if (!string.IsNullOrEmpty(lasthash) && lasthash == hextext)
					useDB2 = false;
				else if (string.IsNullOrEmpty(lasthash))
					hashlist.Add(hextext + filename);
				else
				{
					hashes.Remove(intId);
					newhashlist.Add(hextext + filename);
					hashes.Add(intId, newhashlist);
				}
			}
			else
				hashes.Add(intId, new List<string>() { hextext + filename });

			return useDB2;
		}

		[EnableCors] 
        [HttpGet]
        public IActionResult Get()
		{
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);
			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();

			List<string> dirs = new List<string>();
			
			foreach (var v in Directory.EnumerateDirectories("tmp"))
				dirs.Add(v.Substring(1 + v.IndexOf('/')));
			
			return Ok(dirs);
		}

		class DirsSorter : IComparer<Dirs>
		{
			public int Compare(Dirs x, Dirs y) {
				return x.Name.CompareTo(y.Name);
			}
		}
		public class Dirs
		{
			public string Name { get; set; }
			public string Size { get; set; }
			public string Files { get; set; }
		}

		[EnableCors] 
        [HttpGet("{id}")]
        public IActionResult Get(int id)
		{
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);
			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();

			List<Dirs> dirs = new List<Dirs>();
			
			foreach (var v in Directory.EnumerateDirectories("tmp/" + id))
			{
				var files = Directory.EnumerateFiles(v);
				
				HashSet<string> dict = new HashSet<string>();
				foreach (var file in files)
				{
					string key = file.Substring(file.IndexOf('_') + 1);
					int index = key.LastIndexOf('/');
					if (index != -1)
						key = key.Substring(index + 1);

					if (!dict.Contains(key))
						dict.Add(key);
				}
				string s = "";
				foreach (var dic in dict)
					s += dic + ' ';

				dirs.Add(
					new Dirs(){Name = v.Substring(1 + v.LastIndexOf('/')), 
					Size = files.Count().ToString(),
					Files = s}
					);
			}

			dirs.Sort(new DirsSorter());
			
			return Ok(dirs);
		}

        [HttpDelete("{agentid}/{dirname}")]
        public IActionResult Delete(int agentid, string dirname)
		{
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);

			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();

			try
			{
				Directory.Delete("tmp/"+agentid+"/"+dirname, true);
				return Ok(0);
			}
			catch (Exception ex)
			{	
				System.Console.WriteLine(ex.Message);
				return StatusCode(500);
			}
		}

        [HttpPut("{id}/{resource}")]
        public async Task<System.Net.Http.HttpResponseMessage> PutFile(int id, string resource)
        {
            string username = "";
            if (HttpContext != null && HttpContext.Request != null)
                username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (string.IsNullOrEmpty(username))
				username = ProductsController.getUsername2(HttpContext.Request.Headers["Cookie"]);

			if (username != Startup.Configuration["adminUser"])
				return new HttpResponseMessage(HttpStatusCode.Unauthorized);

            int index = resource.IndexOf('/');
            string profileName = resource.Substring(index + 1);
            string filename = String.Format("log-{0}.txt", profileName);

            if (Request != null && Request.Query.ContainsKey("filename"))
            {
                filename = Request.Query["filename"].ToString();
            }

			bool append = false;
            if (Request != null && Request.Query.ContainsKey("append"))
            {
                append = Request.Query["append"].ToString() == "true";
            }

			ProductsRepo repo = new ProductsRepo();
			if (repo.GetOne(id) == null)
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}
			bool history = repo.GetOne(id).UploadHistory;
			if (filename.EndsWith(".zip"))
				history = false;

			try
			{
				string body = "";
				using (var reader = new StreamReader(Request.Body))
				{
					body = await reader.ReadToEndAsync();
				}

				StringReader sr = new StringReader(body);
				System.Net.Mail.MailMessage mm = Amende.Snorre.MailMessageMimeParser.ParseMessage(sr);

				string datedir = DateTime.Now.ToString("MMddyyyy");
				System.IO.Directory.CreateDirectory("tmp/"+id);
				if (history) System.IO.Directory.CreateDirectory("tmp/"+id + "/" + datedir);
				Console.WriteLine("count=" + mm.Attachments.Count);
				if (mm.Attachments.Count > 0)
				{
					using (var fileStream = System.IO.File.Open(String.Format(@"{0}/{1}/{2}", "tmp", id, filename),
						append ? FileMode.Append : FileMode.Create))
					{
						using (MemoryStream ms = new MemoryStream())
						{
                        	mm.Attachments[0].ContentStream.CopyTo(ms);
                        	ms.Position = 0;
                            ms.CopyTo(fileStream);
							if (append || !checkHash(ms, id, filename))
							{
								history = false;
								//System.Console.WriteLine("dup hash for " + filename);
							}
						}
					}

					if (history) System.IO.File.Copy(String.Format(@"{0}/{1}/{2}", "tmp", id, filename), 
						String.Format(@"{0}/{1}/{2}/{3}_{4}", "tmp", id, datedir, DateTime.Now.Ticks, filename));

					System.IO.Directory.CreateDirectory("wwwroot/WebApp/logs/"+id);
					System.IO.File.WriteAllText("wwwroot/WebApp/logs/" + id + "/" + filename + "_date.txt", DateTime.Now.ToString());
				}
				else
					using (StreamWriter sw = System.IO.File.CreateText("tmp/" + id + "/" + filename))
					{
						sw.Write(mm.Body);
					}
			}
			catch (Exception ex)
			{
				Console.WriteLine("exception" + ex.Message);
			}

			return new HttpResponseMessage(HttpStatusCode.Created);
		}
    }
}
