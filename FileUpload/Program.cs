using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QRCoder;
using System.Drawing;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;

namespace FileUploadServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            var url = "http://" + GetLocalIPAddress() + ":7140";
            //var url = "http://0.0.0.0:7140";
            OpenBrowser(url);
            host.Run();
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("192"))
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., browser not found)
                Console.WriteLine($"Failed to open browser: {ex.Message}");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        services.Configure<FormOptions>(options =>
                        {
                            options.MultipartBodyLengthLimit = 1073741824; // 1GB
                        });
                        services.AddRouting();
                    });

                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Limits.MaxRequestBodySize = 1073741824;
                        serverOptions.ListenAnyIP(7140);
                    }).UseUrls("http://0.0.0.0:7140");

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/", async context =>
                            {
                                string html = @"
<!DOCTYPE html>
<html>
<head>
    <title>File Upload Server</title>
    <style>
        form {
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 20px;
        }

        input[type=""file""] {
            margin-bottom: 10px;
            width: 100%;
            max-width: 400px;
        }

        button {
            padding: 10px 20px;
            background-color: #007bff;
            color: white;
            border: none;
            cursor: pointer;
        }

        button:hover {
            background-color: #0056b3;
        }

        .shutdown-button {
            margin-top: 20px;
            padding: 10px 20px;
            background-color: #dc3545;
        }

        .progress-container {
            width: 100%;
            height: 25px;
            background-color: #e0e0e0;
            border-radius: 5px;
            margin-top: 10px;
        }

        .progress-bar {
            height: 100%;
            width: 0%;
            border-radius: 5px;
            transition: width 0.3s ease;
        }
    </style>
</head>
<body>
    <h1>File Upload Server</h1>
    <form action='/upload' method='post' enctype='multipart/form-data'>
        <input type='file' name='files' multiple />
        <button type='submit'>Upload</button>
    </form>
    <button class='shutdown-button' onclick='shutdownServer()'>Shutdown Server</button>
    <div class=""progress-container"">
        <div id=""progress-bar"" class=""progress-bar""></div>
    </div>
    <div class=""qr-container"">
        <h3>Connect with this QR Code:</h3>
        <img src='/generate-qr?ip={localIpAddress}' alt='QR Code for Server IP' />
    </div>

    <script>
        async function shutdownServer() {
            const response = await fetch('/shutdown', { method: 'POST' });
            if (response.ok) {
                alert('Server is shutting down...');
                window.location.href = '/';
            } else {
                alert('Failed to shutdown server.');
            }
        }

        async function uploadFile(files) {
            const progressBar = document.getElementById('progress-bar');
            const formData = new FormData();
            for (let file of files) {
                formData.append('files', file);
            }

            const xhr = new XMLHttpRequest();
            xhr.open('POST', '/upload', true);
            xhr.upload.onprogress = (event) => {
                if (event.lengthComputable) {
                    const percentComplete = (event.loaded / event.total) * 100;
                    progressBar.style.width = `${percentComplete}%`;

                    // Change color based on progress
                    if (percentComplete < 33) {
                        progressBar.style.backgroundColor = '#f00'; // Red for 0-33%
                    } else if (percentComplete < 66) {
                        progressBar.style.backgroundColor = '#ff0'; // Yellow for 34-66%
                    } else {
                        progressBar.style.backgroundColor = '#0f0'; // Green for 67-100%
                    }
                }
            };

            xhr.onload = () => {
                if (xhr.status === 200) {
                    alert('File upload successful.');
                    progressBar.style.width = '0%';
                    progressBar.style.backgroundColor = '#0f0'; // Reset color after success
                } else {
                    alert('Error during upload');
                }
            };

            xhr.send(formData);
        }

        document.querySelector('form').addEventListener('submit', async (event) => {
            event.preventDefault();
            const files = event.target.elements['files'].files;
            if (files.length > 0) {
                uploadFile(files);
            } else {
                alert('No files selected');
            }
        });
    </script>
</body>
</html>";

                                context.Response.ContentType = "text/html";
                                await context.Response.WriteAsync(html);
                            });

                            endpoints.MapPost("/shutdown", async context =>
                            {
                                context.Response.StatusCode = 200;
                                await context.Response.WriteAsync("Server is shutting down...");
                                var hostApplicationLifetime = context.RequestServices.GetService<IHostApplicationLifetime>();
                                hostApplicationLifetime.StopApplication();
                            });

                            endpoints.MapPost("/upload", async context =>
                            {
                                if (!context.Request.HasFormContentType)
                                {
                                    context.Response.StatusCode = 400;
                                    await context.Response.WriteAsync("Invalid form submission.");
                                    return;
                                }

                                var form = await context.Request.ReadFormAsync();
                                var files = form.Files;

                                if (files.Count == 0)
                                {
                                    context.Response.StatusCode = 400;
                                    await context.Response.WriteAsync("No files uploaded.");
                                    return;
                                }

                                var uploadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                                if (!Directory.Exists(uploadsFolder))
                                {
                                    Directory.CreateDirectory(uploadsFolder);
                                }

                                foreach (var file in files)
                                {
                                    var filePath = Path.Combine(uploadsFolder, file.FileName);
                                    using (var stream = new FileStream(filePath, FileMode.Create))
                                    {
                                        await file.CopyToAsync(stream);
                                    }
                                }

                                string htmlResponse = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <title>File Upload Successful</title>
            <style>
                .form-container {{
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    padding: 20px;
                }}
                
                .button {{
                    padding: 10px 20px;
                    background-color: #dc3545;
                    color: white;
                    border: none;
                    cursor: pointer;
                }}
                
                .button:hover {{
                    background-color: #0056b3;
                }}
            </style>
        </head>
        <body>
            <h1>File Upload Successful</h1>
            <p>{files.Count} files uploaded successfully.</p>
            <button class='button' onclick='shutdownServer()'>Shutdown Server</button>
            
            <script>
                async function shutdownServer() {{
                    const response = await fetch('/shutdown', {{ method: 'POST' }});
                    if (response.ok) {{
                        alert('Server is shutting down...');
                        window.location.href = '/';
                    }} else {{
                        alert('Failed to shutdown server.');
                    }}
                }}
            </script>
        </body>
        </html>";

                                context.Response.ContentType = "text/html";
                                await context.Response.WriteAsync(htmlResponse);
                            });

                            endpoints.MapGet("/generate-qr", async context =>
                            {
                                string localIpAddress = context.Connection.RemoteIpAddress.ToString();

                                if (localIpAddress != null && localIpAddress.StartsWith("::ffff:"))
                                {
                                    localIpAddress = "http://" + localIpAddress.Substring(7) + ":7140";
                                }

                                using (var qrGenerator = new QRCodeGenerator())
                                {
                                    var qrCodeData = qrGenerator.CreateQrCode(localIpAddress, QRCodeGenerator.ECCLevel.Q);
                                    using (var qrCode = new BitmapByteQRCode(qrCodeData))
                                    {
                                        var qrCodeImage = qrCode.GetGraphic(10); // Adjust the size as needed
                                        context.Response.ContentType = "image/png";
                                        await context.Response.Body.WriteAsync(qrCodeImage, 0, qrCodeImage.Length);
                                    }
                                }
                            });
                        });
                    });
                });
    }
}
