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
