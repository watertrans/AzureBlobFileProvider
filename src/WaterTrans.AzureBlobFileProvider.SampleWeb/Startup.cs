using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace WaterTrans.AzureBlobFileProvider.SampleWeb
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // ConnectionString
            var blobOptions = new AzureBlobOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "files",
                LocalCacheTimeout = 300,
            };

            // SAS (ServiceUri + Token)
            /*
            var blobOptions = new AzureBlobOptions
            {
                ServiceUri = new System.Uri("BLOB_SERVICE_SAS_URL"), // ex.) https://youraccount.blob.core.windows.net
                Token = "SAS_TOKEN",                                 // ex.) ?sv=2020-08-04&ss=b&srt=co&sp=rltfx&se=2021-01-01T00:00:00Z&st=2021-01-02T00:00:00Z&spr=https&sig=xxxxxxxxxxxxxxxxxxxxxx%2Bx%2Fxx%2Bxxxxxxxxxxxxxxx%3D
                ContainerName = "files",
                LocalCacheTimeout = 300,
            };
            */

            services.AddSingleton(new AzureBlobFileProvider(blobOptions));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var blobFileProvider = app.ApplicationServices.GetRequiredService<AzureBlobFileProvider>();

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = blobFileProvider,
                RequestPath = "/files",
                OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append("Cache-Control", $"public, max-age=300")
            });

            app.UseDirectoryBrowser(new DirectoryBrowserOptions
            {
                FileProvider = blobFileProvider,
                RequestPath = "/files"
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("<a href='files'>files</a>");
                });
            });
        }
    }
}
