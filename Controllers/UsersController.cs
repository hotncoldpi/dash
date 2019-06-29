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
	public class User
	{
		public string Name { get; set; }
		public int    Count { get; set; }
		public string Ids { get; set; }
		public string Info { get; set; }
	}

    [Route("WebApp/api/[controller]")]
    public class UsersController : Controller
    {
		private string getType(string name)
		{
			if (name == Startup.Configuration["adminUser"])
				return "Administrator";
			
			if (name == Startup.Configuration["readonlyUser"])
				return "Read Only";

			return "Read Write";
		}

        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody]User u)
		{
			string username = "";
			if (HttpContext != null && HttpContext.Request != null)
				username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			
			if (u.Name.IndexOf('"') != -1 || u.Ids.IndexOf('"') != -1)
				return Ok(-2);
			
			try
			{
				Process p = Process.Start("htpasswd", "-bv /etc/apache2/.htpasswd " + username + " \"" + u.Name + "\"");
				p.WaitForExit();
				if (p.ExitCode != 0)
					return Ok(-1);					
			}
			catch (Exception ex)
			{
				Console.WriteLine("Put exception: " + ex.Message);				
			}
			
			try
			{
				Process p = Process.Start("htpasswd", "-b /etc/apache2/.htpasswd " + username + " \"" + u.Ids + "\"");
				p.WaitForExit();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Put exception: " + ex.Message);				
			}
			
			return Ok(0);
		}
		
        [HttpPost]
        public IActionResult Post(User u)
		{
			string username = "";
			if (HttpContext != null && HttpContext.Request != null)
				username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();
			
			Console.WriteLine("user=" + u.Name);
			try
			{
				Process p = Process.Start("htpasswd", "-b /etc/apache2/.htpasswd " + u.Name + " " + u.Info);
				p.WaitForExit();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Post exception: " + ex.Message);				
			}
			
			return Ok(u);
		}

        [HttpDelete("{id}")]
        public IActionResult Delete(int id, [FromBody]User u)
        {
			string username = "";
			if (HttpContext != null && HttpContext.Request != null)
				username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (username != Startup.Configuration["adminUser"])
				return Unauthorized();
			
			try
			{
				Process p = Process.Start("htpasswd", "-bD /etc/apache2/.htpasswd " + u.Name + " x");
				p.WaitForExit();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Delete exception: " + ex.Message);				
			}
			
			return Ok(1);
        }
		
        [HttpGet]
        public IEnumerable<User> Get()
        {
            List<User> users = new List<User>();
			//users.Add(new User(){Name = "test", Ids = "", Info = "Read Only"});
			
			string username = "";
			if (HttpContext != null && HttpContext.Request != null)
				username = ProductsController.getUsername(HttpContext.Request.Headers["Authorization"]);
			if (username != Startup.Configuration["adminUser"])
			{
				users.Add(new User(){Name = username, Ids = "", Info = getType(username)});
				return users;
			}
			
			try
			{
				using (StreamReader sr = System.IO.File.OpenText("/etc/apache2/.htpasswd"))
				{
					while (!sr.EndOfStream)
					{
						string s = sr.ReadLine();
						if (s.IndexOf(':') != -1)
							s = s.Substring(0, s.IndexOf(':'));
						users.Add(new User(){Name = s, Ids = "", Info = getType(s)});
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("exception: " + ex.Message);
			}
			
            return users;
        }
    }
}
