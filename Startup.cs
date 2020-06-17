using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace PokeApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options => 
            {
                options.AddDefaultPolicy(builder => builder.WithOrigins("*"));
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseRouting();

            app.UseCors();

            app.Use(next => context =>
            {
                logger.LogInformation($"{context.Request.Method} {context.Request.Path}");
                return next(context);
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/pokemon", async context =>
                {
                    using IDbConnection db = new NpgsqlConnection(Configuration["ConnectionString"]);

                    var pokemon = await db.QueryAsync("SELECT id, name FROM pokemon");

                    await JsonSerializer.SerializeAsync(context.Response.Body, pokemon);
                });

                endpoints.MapGet("/pokemon/{id:int}", async context =>
                {
                    const string SelectName = @"
                        SELECT name
                        FROM pokemon
                        WHERE id = @Id
                    ";

                    const string SelectTypes = @"
                        SELECT t.name 
                        FROM type t
                        JOIN pokemon_type pt ON t.id = pt.type_id 
                        WHERE pt.pokemon_id = @Id
                    ";

                    var id = int.Parse(context.Request.RouteValues["id"].ToString());

                    using IDbConnection db = new NpgsqlConnection(Configuration["ConnectionString"]);

                    var name = await db.QuerySingleOrDefaultAsync<string>(SelectName, new { Id = id });

                    if (name is null)
                    {
                        context.Response.StatusCode = 404;

                        return;
                    }

                    var types = await db.QueryAsync<string>(SelectTypes, new { Id = id });

                    await JsonSerializer.SerializeAsync(context.Response.Body, new
                    {
                        id,
                        name,
                        types
                    });
                });
            });
        }
    }
}
