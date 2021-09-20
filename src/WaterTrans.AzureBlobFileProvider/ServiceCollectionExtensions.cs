using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using WaterTrans.AzureBlobFileProvider;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up azure blob file provider services in an <see cref="IServiceCollection" />.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a default implementation for the <see cref="AzureBlobFileProvider"/> service.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="setupAction">An <see cref="Action{AzureBlobOptions}"/> to configure the provided <see cref="AzureBlobOptions"/>.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAzureBlobFileProvider(this IServiceCollection services, Action<AzureBlobOptions> setupAction)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            services.AddOptions();
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.TryAddSingleton<AzureBlobFileProvider>();
            services.Configure(setupAction);
            return services;
        }
    }
}
