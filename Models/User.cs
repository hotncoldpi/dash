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

namespace TodoApi.Models
{
	public class User
	{
		public string Name { get; set; }
		public int    Count { get; set; }
		public string Ids { get; set; }
		public string Info { get; set; }
	}
}
