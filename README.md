# **UPDATE**
I archived this project in favor of the Microsoft.Extensions.Caching.Postgres library: https://www.nuget.org/packages/Microsoft.Extensions.Caching.Postgres

More info: https://techcommunity.microsoft.com/blog/azuredevcommunityblog/postgres-as-a-distributed-cache-unlocks-speed-and-simplicity-for-modern-net-work/4462139

# Extensions.Caching.PostgreSQL
Distributed cache implementation of IDistributedCache using PostgreSQL

## Getting Started
1. Install the package into your project
```
dotnet add package Extensions.Caching.PostgreSql
```
2. Add the following line to the `Startup`  `Configure` method.
```c#
services.AddDistributedPostgreSqlCache(options => 
{
	options.ConnectionString = configuration["ConnectionString"];
	options.SchemaName = configuration["SchemaName"];
	options.TableName = configuration["TableName"];
})
```

Available on NuGet at [https://www.nuget.org/packages/Extensions.Caching.PostgreSql/](https://www.nuget.org/packages/Extensions.Caching.PostgreSql/)
