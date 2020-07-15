using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTestLargeHttpPayload
{
    class Program
    {
        private const string DelegatedSubnetMonikerHeader = "x-ms-ppvnet-delegated-subnet-moniker";

        private readonly HttpClient client = new HttpClient();
        private readonly string baseUrl;
        private readonly string delegatedSubnetMoniker;

        public Program(string baseUrl, string delegatedSubnetMoniker)
        {
            this.baseUrl = baseUrl;
            this.delegatedSubnetMoniker = delegatedSubnetMoniker;
            client.Timeout = Timeout.InfiniteTimeSpan;
        }

        public static void Main(string[] args)
        {
            ServicePointManager.CertificatePolicy = new TrustAllCertsPolicy();

            Parser.Default.ParseArguments<Options>(args)
                     .WithParsed<Options>(o =>
                     {
                         var program = new Program(o.BaseUrl, o.DelegatedSubnetMoniker);
                         //program.Run(); return;

                         if (string.Equals(o.Method, "Get", StringComparison.InvariantCultureIgnoreCase))
                         {
                             program.TestGet(o.ReceiveBytes).Wait();
                         }
                         else if (string.Equals(o.Method, "Post", StringComparison.InvariantCultureIgnoreCase))
                         {
                             program.TestPost(o.SendBytes, o.ReceiveBytes).Wait();
                         }
                         else
                         {
                             throw new ArgumentException(nameof(o.Method));
                         }
                     });
        }

        public class Options
        {
            [Option('u', "url", Required = false, HelpText = "Base URL", Default = "http://localhost:13765")]
            public string BaseUrl { get; set; }

            [Option('d', "delegated", Required = false, HelpText = "Delegated Subnet Moniker", Default = "add696cb-69f0-484e-bb76-a374195d32c7")]
            public string DelegatedSubnetMoniker { get; set; }

            [Option('m', "method", Required = true, HelpText = "HTTP method")]
            public string Method { get; set; }

            [Option('s', "send", Required = false, Default = 0, HelpText = "Bytes to send")]
            public long SendBytes { get; set; }

            [Option('r', "receive", Required = false, Default = 0, HelpText = "Bytes to receive")]
            public long ReceiveBytes { get; set; }
        }

        public class TrustAllCertsPolicy : ICertificatePolicy
        {
            public bool CheckValidationResult(
                ServicePoint srvPoint, X509Certificate certificate,
                WebRequest request, int certificateProblem)
            {
                return true;
            }
        }

        private async Task TestGet(long bytesToRead)
        {
            Console.WriteLine($"Gonna read {bytesToRead} bytes");

            try
            {

                HttpRequestMessage getRequest = BuildGetRequest(bytesToRead);
                HttpResponseMessage response = await this.client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Got {response.StatusCode}");
                    throw new Exception();
                }
                Console.WriteLine(response.Headers);

                Stream stream = await response.Content.ReadAsStreamAsync();

                long totalBytesRead = ReadStream(stream);

                if (totalBytesRead != bytesToRead)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
            }
        }

        private async Task TestPost(long bytesToSend, long bytesToReceive)
        {
            Console.WriteLine($"Gonna send {bytesToSend} bytes");
            try
            {
                Stream stream = new RandomReadOnlyStream(bytesToSend);

                HttpRequestMessage getRequest = BuildPostRequest(bytesToReceive, stream);
                HttpResponseMessage response = await this.client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                foreach (var header in response.Headers)
                {
                    Console.WriteLine($"{header.Key}={string.Join(",", header.Value)}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Got {response.StatusCode}");
                    throw new Exception();
                }

                Stream responseStream = await response.Content.ReadAsStreamAsync();

                long totalBytesRead = ReadStream(responseStream);

                if (totalBytesRead != bytesToReceive)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
            }
        }

        private HttpRequestMessage BuildGetRequest(long bytesToRead)
        {
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri($"{this.baseUrl}/random/{bytesToRead}");
            request.Method = HttpMethod.Get;
            request.Headers.Add(DelegatedSubnetMonikerHeader, this.delegatedSubnetMoniker);
            return request;
        }

        private HttpRequestMessage BuildPostRequest(long bytesToReceive, Stream stream)
        {
            var request = new HttpRequestMessage();
            request.RequestUri = new Uri($"{this.baseUrl}/random/{bytesToReceive}");
            request.Method = HttpMethod.Post;
            request.Headers.Add(DelegatedSubnetMonikerHeader, this.delegatedSubnetMoniker);
            request.Content = new StreamContent(stream);
            return request;
        }

        private long ReadStream(Stream stream)
        {
            long totalBytesRead = 0;
            byte[] buffer = new byte[1024*1024];
            bool skipPrinting = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead <= 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                double seconds = stopwatch.Elapsed.TotalSeconds;
                Console.WriteLine($"totalBytesRead {totalBytesRead}, {seconds} sec, {totalBytesRead / 1024 / 1024 / seconds} MB/s");

                string s = Encoding.Default.GetString(buffer, 0, bytesRead);
                if (totalBytesRead <= 1024)
                {
                    Console.Write(s);
                }
                else if (!skipPrinting)
                {
                    Console.WriteLine("...");
                    skipPrinting = true;
                }
            }

            Console.WriteLine($"Total bytes: {totalBytesRead}");
            Console.WriteLine();
            return totalBytesRead;
        }
    }
}
