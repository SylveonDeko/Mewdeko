using Mewdeko.Votes.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Mewdeko.Votes;

public class Startup
{
    public Startup(IConfiguration configuration) => Configuration = configuration;

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddSingleton<FileVotesCache>();
        services.AddSingleton<WebhookEvents>();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Mewdeko.Votes", Version = "v1" });
        });

        services
            .AddAuthentication(opts =>
            {
                opts.DefaultScheme = AuthHandler.SCHEME_NAME;
                opts.AddScheme<AuthHandler>(AuthHandler.SCHEME_NAME, AuthHandler.SCHEME_NAME);
            });
            
        services
            .AddAuthorization(opts =>
            {
                opts.DefaultPolicy = new AuthorizationPolicyBuilder(AuthHandler.SCHEME_NAME)
                                     .RequireAssertion(x => false)
                                     .Build();
                opts.AddPolicy(Policies.DISCORDS_AUTH, policy => policy.RequireClaim(AuthHandler.DISCORDS_CLAIM));
                opts.AddPolicy(Policies.TOPGG_AUTH, policy => policy.RequireClaim(AuthHandler.TOPGG_CLAIM));
            });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mewdeko.Votes v1"));
        }

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}