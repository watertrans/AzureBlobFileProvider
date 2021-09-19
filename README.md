# AzureBlobFileProvider
Azure Blob Storage file provider (IFileProvider) for ASP.NET Core.

### Installation
```
Install-Package WaterTrans.AzureBlobFileProvider
```

### Features
- Serve the files in the blob container as static files.
- Download the blob item to the local cache and respond from the local cache for the specified number of seconds.
- The location of the local cache directory can be specified.

### Usage

Configure access to your Blob Storage via storage account connection string or SAS token. 

Below is the usage example for both flows - where access to files from Blob Storage is enabled on the `/files` route (including directory browsing in the browser).

**Connection string**

```csharp
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var blobOptions = new AzureBlobOptions
            {
                ConnectionString = "UseDevelopmentStorage=true",
                ContainerName = "files",
            };
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
```

**Token** (need to provide the URL of the storage separately)

```csharp
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var blobOptions = new AzureBlobOptions
            {
                ServiceUri = new System.Uri("BLOB_SERVICE_SAS_URL"), // ex.) https://youraccount.blob.core.windows.net
                Token = "SAS_TOKEN",                                 // ex.) ?sv=2020-08-04&ss=b&srt=co&sp=rltfx&se=2021-01-01T00:00:00Z&st=2021-01-02T00:00:00Z&spr=https&sig=xxxxxxxxxxxxxxxxxxxxxx%2Bx%2Fxx%2Bxxxxxxxxxxxxxxx%3D
                ContainerName = "files",
            };
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
```

In case both `ConnectionString` and `Token` are present, connection string is given the preference.

### Note

