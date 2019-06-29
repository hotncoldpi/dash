using System;
using System.Security.Cryptography;
using System.Runtime.Serialization;

namespace TodoApi.Models
{
    public class Product : IComparable<Product>
    {
        public int      Id {get; set;}
        public int      Age {get; set;}
        public string   Name {get; set;}
        public string   Desc {get; set;}
        public string   OS {get; set;}
        public string   IP {get; set;}
        public string   IP2 {get; set;}
        public string   Output {get; set;}
        public string   OutputHash {get; set;}
        public string   Profile {get; set;}
        public string   FirstLine {get; set;}
        public string   Version {get; set;}
        public string   Status {get; set;}
        public int      Percent {get; set;}
        public DateTime LastReport {get;set;}
        public string   Command{ get; set; }
        public string   Response { get; set; }
        public bool     AlertCondition { get; set; }
        
		[IgnoreDataMember]
        public string   SecondLine {get; set;}
		
		public Product Clone() 
        { 
            Product p = MemberwiseClone() as Product;
            return p; 
        }

		public int CompareTo(Product comparePart)
        {
            // A null value means that this object is greater.
            if (comparePart == null)
                return 1;
            else
                return this.Name.CompareTo(comparePart.Name);
        }
 
		private string getFirstLine(string[] lines)
		{
			//format:
			//profile name
			//alert message
			//URL count
			//URL(s)
			//pic URLs		- same number of lines as output lines
			//output lines	- same number of lines as pic URLs
			int urlCount = Convert.ToInt32(lines[2]);
			int startOfPics = 3 + urlCount;
			int offset = lines.Length - startOfPics;
			
			int startOfData = startOfPics + offset/2;
			string firstLine = lines[startOfData];
			
			int colon = firstLine.IndexOf(':');
			if (colon != -1)
				firstLine = firstLine.Substring(colon + 1);
			
			if (startOfData + 1 < lines.Length) 
			{
				SecondLine = lines[startOfData + 1];
				colon = SecondLine.IndexOf(':');
				if (colon == 1)
					SecondLine = SecondLine.Substring(colon + 1);
			}
			
			return firstLine;
		}

        public void setOutput(string output)
		{
			Output = output;
			
			try
			{
				//compute hash of output
				byte[] bytes = System.Text.Encoding.ASCII.GetBytes(Output ?? "");
				bytes = SHA1.Create().ComputeHash(bytes);
				string hextext = BitConverter.ToString(bytes).Replace("-", "").ToLower();
				
				OutputHash = hextext;
			
				//base64 decode and split by line
				byte[] bites = Convert.FromBase64String(Output);
				string decoded = System.Text.Encoding.ASCII.GetString(bites);
				string[] lines = decoded.Split('\n');

				string firstLine = getFirstLine(lines);
				bool alertCondition = lines[1].Length > 0;
				
				//set properties
				AlertCondition = alertCondition;
				FirstLine = firstLine;
			}
			catch (Exception)
			{
				OutputHash = "";
				AlertCondition = false;
				FirstLine = "";
			}
		}
		
        public Product setAge()
        {
			try
			{
				TimeSpan ts = DateTime.Now - LastReport;
				Age = Convert.ToInt32(ts.TotalSeconds);
			}
			catch (Exception)
			{
				Age = Int32.MaxValue;
			}

			Output = SecondLine ?? "";
			return this;
        }
    }
}
