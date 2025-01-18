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
using System.Reflection;

namespace FileUpload.wwwroot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            var url = "http://" + GetLocalIPAddress() + ":7140";
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
                        serverOptions.Listen(IPAddress.Any, 7140);
                    }).UseUrls("http://0.0.0.0:7140");

                    webBuilder.Configure(app =>
                    {
                        app.UseStaticFiles();
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Serve static files from the AllowDownloads folder
                            var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AllowDownloads");
                            if (!Directory.Exists(downloadsFolder))
                            {
                                Directory.CreateDirectory(downloadsFolder);
                            }

                            app.UseStaticFiles(new StaticFileOptions
                            {
                                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(downloadsFolder),
                                RequestPath = "/files-preview"
                            });

                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/files", async context =>
                                {
                                    var test = Assembly.GetExecutingAssembly().GetName().Name;
                                    var files = Directory.GetFiles(downloadsFolder);
                                    string html = await File.ReadAllTextAsync("wwwroot/files.html");
                                    string filePreviews = string.Empty;

                                    foreach (var file in files)
                                    {
                                        var fileName = Path.GetFileName(file);
                                        var fileExtension = Path.GetExtension(file).ToLower();
                                        string preview;

                                        if (fileExtension == ".jpg" || fileExtension == ".png" || fileExtension == ".gif")
                                        {
                                            preview = $"<img src='/files-preview/{Uri.EscapeDataString(fileName)}' alt='{fileName}'>";
                                        }
                                        else
                                        {
                                            preview = $"<div class='icon'>📄</div>"; // Placeholder icon for non-images
                                        }

                                        filePreviews += $@"
                    <div class='file-card'>
                        <div class='file-preview'>{preview}</div>
                        <div class='file-name'>{fileName}</div>
                        <a href='/download?fileName={Uri.EscapeDataString(fileName)}' class='download-btn'>Download</a>
                    </div>";
                                    }

                                    html = html.Replace("{{FilePreviews}}", filePreviews);
                                    context.Response.ContentType = "text/html";
                                    await context.Response.WriteAsync(html);
                                });
                            });

                            endpoints.MapGet("/download", async context =>
                            {
                                var fileName = context.Request.Query["fileName"].ToString();
                                var filePath = Path.Combine(downloadsFolder, fileName);

                                if (File.Exists(filePath))
                                {
                                    context.Response.ContentType = "application/octet-stream";
                                    context.Response.Headers.Add("Content-Disposition", $"attachment; filename={Uri.EscapeDataString(fileName)}");

                                    await context.Response.SendFileAsync(filePath);
                                }
                                else
                                {
                                    context.Response.StatusCode = 404;
                                    await context.Response.WriteAsync("File not found.");
                                }
                            });
                            endpoints.MapGet("/", async context =>
                        {
                            string html = await File.ReadAllTextAsync("wwwroot/index.html");
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

                                string htmlResponse = await File.ReadAllTextAsync("wwwroot/upload-success.html");
                                htmlResponse = htmlResponse.Replace("{{FileCount}}", files.Count.ToString());

                                context.Response.ContentType = "text/html";
                                await context.Response.WriteAsync(htmlResponse);
                            });

                            endpoints.MapGet("/generate-qr", async context =>
                            {
                                string localIpAddress = context.Connection.RemoteIpAddress.ToString();

                                if (localIpAddress != null && localIpAddress.StartsWith("::ffff:"))
                                {
                                    localIpAddress = localIpAddress.Substring(7);
                                }
                                localIpAddress = "http://" + localIpAddress + ":7140";
                                using (var qrGenerator = new QRCodeGenerator())
                                {
                                    var qrCodeData = qrGenerator.CreateQrCode(localIpAddress, QRCodeGenerator.ECCLevel.Q);
                                    using (var qrCode = new BitmapByteQRCode(qrCodeData))
                                    {
                                        var qrCodeImage = qrCode.GetGraphic(10);
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
