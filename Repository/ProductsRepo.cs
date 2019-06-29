using System;
using System.IO;
using System.Collections.Generic;
using TodoApi.Models;

namespace TodoApi.Repository
{
    public class ProductsRepo
    {
		//this won't work in load-balanced server environment!
        static Dictionary<int, Product> products = new Dictionary<int, Product>();
		
		public ProductsRepo()
		{
		}

		public int getNextId()
		{
            lock (products)
            {
				int key = 0;
				foreach (var v in products.Keys)
					if (v > key) 
						key = v;
					
				return key + 1;
			}
		}
		
		public Product GetOne(int id)
		{
            lock (products)
            {
                if (!products.ContainsKey(id))
                    return null;
                
				return products[id];
			}
		}
		
		public List<Product> GetAll(int age)
		{
            List<Product> newProds = new List<Product>();
				
			lock (products)
			{
				foreach (var v in products.Values)
				{
					Product cloned = v.Clone().setAge();
					if (age == -1 || cloned.Age < age)
						newProds.Add(cloned);
				}
			}
			
			return newProds;
		}
		
		public void SetOne(int id, Product p, string ip)
		{
			lock (products)
			{
			}
		}

		public void SetAll(List<Product> p)
		{
			lock (products)
			{
    		    products.Clear();
				foreach (Product p0 in p)
				{
					p0.SecondLine = p0.Output;
					p0.Output = "";
					products.Add(p0.Id, p0);
				}
			}
			Save();
		}
		
		public void AddOne(int id, Product p)
		{
			if (id == -1)
				id = getNextId();
			p.Id = id;
			
			lock (products)
			{
                if (products.ContainsKey(id))
					return;
				products.Add(id, p);
			}
			//TODO: append instead of resaving whole list
			Save();
		}
		
		public void DeleteOne(int id)
		{
			lock (products)
			{
                if (!products.ContainsKey(id))
					return;
				products.Remove(id);
			}
			Save();
		}

		public void Save()
		{
			lock (products)
			{
				using (StreamWriter sw = System.IO.File.CreateText("ids.txt"))
				{
					foreach (Product p in products.Values)
					{
						if (p.Id != 0)
							sw.WriteLine(p.Id + ":" + p.Name + ":" + (p.Version ?? "") + ":" + (p.Desc ?? ""));
					}
				}
			}			
		}
		
		public void Load()
		{
			try
			{
				using (StreamReader sr = System.IO.File.OpenText("ids.txt"))
				{
					lock(products)
					{
						products.Clear();
						
						while (!sr.EndOfStream)
						{
							string line = sr.ReadLine();
							char[] seps = new char[]{':'};
							string[] toks = line.Split(seps);
							if (toks.Length >= 2) 
							{
								int newId = Convert.ToInt32(toks[0]);
								string version = null;
								string desc = null;
								if (toks.Length >= 3)
									version = toks[2];
								if (toks.Length >= 4)
									desc = toks[3];
								Product newProd = new Product(){Id = newId, Name = toks[1], Version = version, Desc = desc};
								products.Add(newId, newProd);
							}
						}
					}

				}
			}
			catch (Exception)
			{
				
			}
		}
    }
}
