using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
#if !NET35
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using System.Text.RegularExpressions;
using System.Threading;
using Aliyun.Api;
using Aliyun.Api.DNS.DNS20150109.Request;

namespace Kaedei.AliyunDDNSClient
{
	class Program
	{
		private static void Main(string[] args)
		{
			try
			{
				var configs = File.ReadAllLines("config.txt");
				var accessKeyId = configs[0].Trim(); //Access Key ID，如 DR2DPjKmg4ww0e79
				var accessKeySecret = configs[1].Trim(); //Access Key Secret，如 ysHnd1dhWvoOmbdWKx04evlVEdXEW7 
				var domainName = configs[2].Trim(); //域名，如 google.com
				var rr = configs[3].Trim(); //子域名，如 www
				println("正在更新记录：" + rr + "，域名：" + domainName);

				var aliyunClient = new DefaultAliyunClient("http://dns.aliyuncs.com/", accessKeyId, accessKeySecret);
				var req = new DescribeDomainRecordsRequest() { DomainName = domainName };
				var response = aliyunClient.Execute(req);

				var updateRecord = response.DomainRecords.FirstOrDefault(rec => rec.RR == rr && rec.Type == "A");
				if (updateRecord == null)
					return;
				println("目前域名IP：" + updateRecord.Value);

				//获取IP
#if NET35
				var ipRequest = (HttpWebRequest) WebRequest.Create(ConfigurationManager.AppSettings["IpServer"]);
				ipRequest.AutomaticDecompression = DecompressionMethods.None | DecompressionMethods.GZip |
				                                 DecompressionMethods.Deflate;
				ipRequest.UserAgent = "aliyun-ddns-client-csharp";
				string htmlSource;
				using (var ipResponse = ipRequest.GetResponse())
				{
					using (var responseStream = ipResponse.GetResponseStream())
					{
						using (var streamReader = new StreamReader(responseStream))
						{
							htmlSource = streamReader.ReadToEnd();
						}
					}
				}
#else
				var httpClient = new HttpClient(new HttpClientHandler
				{
					AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.None,
				});
				httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("aliyun-ddns-client-csharp")));
				var htmlSource = httpClient.GetStringAsync(ConfigurationManager.AppSettings["IpServer"]).Result;
#endif
				var ip = Regex.Match(htmlSource, @"((?:(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d)))\.){3}(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d))))", RegexOptions.IgnoreCase).Value;
				println("本机IP地址：" + ip);

				if (updateRecord.Value != ip)
				{
					var changeValueRequest = new UpdateDomainRecordRequest()
					{
						RecordId = updateRecord.RecordId,
						Value = ip,
						Type = "A",
						RR = rr
					};
					aliyunClient.Execute(changeValueRequest);
					println("IP地址更新完成。");
				}
				else
				{
					println("IP地址无变化，退出。");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			//Thread.Sleep(5000);
		}

        public static void println(String str) {
            Console.WriteLine("[" + DateTime.Now.ToLongTimeString().ToString() + " INFO] " + str);
        }
	}
}