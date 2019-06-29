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
    [Route("WebApp/api/[controller]")]
    public class ServersController : Controller
    {
        public const int DEFAULT_HISTORY_TIMESPAN = 600;
		
        class Server
        {
            public int ReplayFrequencySeconds { get; set; }
            public string OS { get { return "Linux"; }  }
        }
		
        [HttpGet]
        public IEnumerable<string> Get()
        {
            List<string> ips = new List<string>();
            return ips;
        }
        [HttpGet("{id}")]
        public IActionResult Get(int id)
		{
            if (id == 0)
                return Ok(new Server() { ReplayFrequencySeconds = DEFAULT_HISTORY_TIMESPAN });

            return NotFound();
		}
    }
}
