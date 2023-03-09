using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MoraChangeTest
{
    class Program
    {
        static void Main(string[] args)
        {
            long lastProcessed = -1;

            string apiBase = "https://p83w4a9vg4.execute-api.us-east-1.amazonaws.com";
            string db = "precisioncountertops";
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(apiBase);

            // Add headers
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Properties.Settings.Default.JWT);

            // Get newest
            {
                HttpResponseMessage response = client.GetAsync($"/changes/{db}/newest").Result;
                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().Result;
                    // I prefer to use ExpandoObjects until the schema is pretty settled - then we can use actual classes
                    dynamic newest = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());
                    lastProcessed = newest.item.knownAsOf;
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    throw new Exception("Unsuccessful GET");
                }
            }

            while (lastProcessed > 0)
            {
                HttpResponseMessage response = client.GetAsync($"/changes/{db}/job/{lastProcessed}").Result;
                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().Result;
                    // I prefer to use ExpandoObjects until the schema is pretty settled - then we can use actual classes
                    dynamic result = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());
                    if (result.count == 0)
                    {
                        Console.WriteLine("Sleeping for 60 seconds until next change is available");
                        System.Threading.Thread.Sleep(60000);
                        continue;
                    }
                    foreach(dynamic item in result.items)
                    {
                        Console.WriteLine($"Job {item.id} changed at {item.lastChanged}");
                        lastProcessed = item.knownAsOf; // typically you would set this after you were "done" with the job, for example, writing something to a db
                    }
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                    throw new Exception("Unsuccessful GET");
                }

            }

            // Dispose once all HttpClient calls are complete. This is not necessary if the containing object will be disposed of; for example in this case the HttpClient instance will be disposed automatically when the application terminates so the following call is superfluous.
            client.Dispose();
        }
    }
}
