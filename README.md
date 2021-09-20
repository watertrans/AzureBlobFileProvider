# AzureBlobFileProvider
Azure Blob Storage file provider (IFileProvider) for ASP.NET Core.

### Installation
```
Install-Package WaterTrans.AzureBlobFileProvider
```

### Features
- Serve the files in the blob container as static files.
- Download the blob item to the local cache and respond from the local cache for the specified number of seconds.
- The local cache can be ignored by query parameter.
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
            // ConnectionString
            services.AddAzureBlobFileProvider(options =>
            {
                options.ConnectionString = "UseDevelopmentStorage=true";
                options.ContainerName = "files";
                options.LocalCacheTimeout = 300;
            });
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
            // SAS (ServiceUri + Token)
            services.AddAzureBlobFileProvider(options =>
            {
                options.ServiceUri = new System.Uri("https://youraccount.blob.core.windows.net");
                options.Token = "?sv=2020-08-04&ss=b&srt=co&sp=rltfx&se=2021-01-01T00:00:00Z&st=2021-01-02T00:00:00Z&spr=https&sig=xxxxxxxxxxxxxxxxxxxxxx%2Bx%2Fxx%2Bxxxxxxxxxxxxxxx%3D";
                options.ContainerName = "files";
                options.LocalCacheTimeout = 300;
            });
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

- Blob items that contain invalid file path characters will be escaped and stored in the local cache.
- If the local cache directory is omitted, the following directory will be used.  
  ``Path.Combine(Path.GetTempPath(), "AzureBlobFileProvider", StorageAccountName, ContainerName)``
- You can ignore the local cache by giving the query parameter as follows.  
  ``/files/favicon.ico?ignoreCache=true``
