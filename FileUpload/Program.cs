using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileUploadServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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
                                string html = @"<!DOCTYPE html>
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
    </style>
    <html>
    <head>
        <title>File Upload Server</title>
    </head>
    <body>
        <h1>File Upload Server</h1>
        <form action='/upload' method='post' enctype='multipart/form-data'>
            <input type='file' name='files' multiple />
            <button type='submit'>Upload</button>
        </form>
        <button class='shutdown-button' onclick='shutdownServer()'>Shutdown Server</button>
        
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
                        });
                    });
                });
    }
}
