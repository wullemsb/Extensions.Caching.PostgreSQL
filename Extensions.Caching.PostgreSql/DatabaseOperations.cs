// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Npgsql;
using System;
using System.Data;
using System.Threading.Tasks;

namespace Extensions.Caching.PostgreSql
{
    internal class DatabaseOperations : IDatabaseOperations
    {
        protected const string GetTableSchemaErrorText =
            "Could not retrieve information of table with schema '{0}' and " +
            "name '{1}'. Make sure you have the table setup and try again. " +
            "Connection string: {2}";

        public DatabaseOperations(
            string connectionString, string schemaName, string tableName, ISystemClock systemClock)
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            TableName = tableName;
            SystemClock = systemClock;
        }

        protected string ConnectionString { get; }

        protected string SchemaName { get; }

        protected string TableName { get; }

        protected ISystemClock SystemClock { get; }

        public void DeleteCacheItem(string key)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var command = new NpgsqlCommand(Functions.Names.DeleteCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key);

                    connection.Open();

                    command.ExecuteNonQuery();
                }
            }
        }

        public async Task DeleteCacheItemAsync(string key)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var command = new NpgsqlCommand(Functions.Names.DeleteCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key);

                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public virtual byte[] GetCacheItem(string key)
        {
            return GetCacheItem(key, includeValue: true);
        }

        public virtual async Task<byte[]> GetCacheItemAsync(string key)
        {
            return await GetCacheItemAsync(key, includeValue: true);
        }

        public void RefreshCacheItem(string key)
        {
            GetCacheItem(key, includeValue: false);
        }

        public async Task RefreshCacheItemAsync(string key)
        {
            await GetCacheItemAsync(key, includeValue: false);
        }

        public virtual void DeleteExpiredCacheItems()
        {
            var utcNow = SystemClock.UtcNow;

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var command = new NpgsqlCommand(Functions.Names.DeleteExpiredCacheItemsFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    connection.Open();

                    command.ExecuteNonQuery();
                }
            }
        }

        public virtual void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var utcNow = SystemClock.UtcNow;

            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var upsertCommand = new NpgsqlCommand(Functions.Names.SetCache, connection))
                {
                    upsertCommand.CommandType = CommandType.StoredProcedure;
                    upsertCommand.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key)
                        .AddCacheItemValue(value)
                        .AddSlidingExpirationInSeconds(options.SlidingExpiration)
                        .AddAbsoluteExpiration(absoluteExpiration)
                        .AddParamWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    connection.Open();

                    try
                    {
                        upsertCommand.ExecuteNonQuery();
                    }
                    catch (PostgresException ex)
                    {
                        if (IsDuplicateKeyException(ex))
                        {
                            // There is a possibility that multiple requests can try to add the same item to the cache, in
                            // which case we receive a 'duplicate key' exception on the primary key column.
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }

        public virtual async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            var utcNow = SystemClock.UtcNow;

            var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
            ValidateOptions(options.SlidingExpiration, absoluteExpiration);

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var upsertCommand = new NpgsqlCommand(Functions.Names.SetCache, connection))
                {
                    upsertCommand.CommandType = CommandType.StoredProcedure;
                    upsertCommand.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key)
                        .AddCacheItemValue(value)
                        .AddSlidingExpirationInSeconds(options.SlidingExpiration)
                        .AddAbsoluteExpiration(absoluteExpiration)
                        .AddParamWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    await connection.OpenAsync();

                    try
                    {
                        await upsertCommand.ExecuteNonQueryAsync();
                    }
                    catch (PostgresException ex)
                    {
                        if (IsDuplicateKeyException(ex))
                        {
                            // There is a possibility that multiple requests can try to add the same item to the cache, in
                            // which case we receive a 'duplicate key' exception on the primary key column.
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }

        protected virtual byte[] GetCacheItem(string key, bool includeValue)
        {
            var utcNow = SystemClock.UtcNow;

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var command = new NpgsqlCommand(Functions.Names.UpdateCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key)
                        .AddWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                if (!includeValue)
                {
                    return null;
                }

                using (var command = new NpgsqlCommand(Functions.Names.GetCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key)
                        .AddWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    using (var reader = command.ExecuteReader(
                        CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult))
                    {
                        if (reader.Read())
                        {
                            return reader.GetFieldValue<byte[]>(Columns.Indexes.CacheItemValueIndex);
                        }
                    }
                }
            }

            return null;
        }

        protected virtual async Task<byte[]> GetCacheItemAsync(string key, bool includeValue)
        {
            var utcNow = SystemClock.UtcNow;

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                using (var command = new NpgsqlCommand(Functions.Names.UpdateCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                       .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                       .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                       .AddCacheItemId(key)
                       .AddWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }

                if (!includeValue)
                {
                    return null;
                }

                using (var command = new NpgsqlCommand(Functions.Names.GetCacheItemFormat, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters
                        .AddParamWithValue("SchemaName", NpgsqlTypes.NpgsqlDbType.Text, SchemaName)
                        .AddParamWithValue("TableName", NpgsqlTypes.NpgsqlDbType.Text, TableName)
                        .AddCacheItemId(key)
                        .AddWithValue("UtcNow", NpgsqlTypes.NpgsqlDbType.TimestampTZ, utcNow);

                    using (var reader = await command.ExecuteReaderAsync(
                        CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult))
                    {
                        if (await reader.ReadAsync())
                        {
                            return await reader.GetFieldValueAsync<byte[]>(Columns.Indexes.CacheItemValueIndex);
                        }
                    }
                }
            }

            return null;
        }

        protected bool IsDuplicateKeyException(PostgresException ex)
        {
            return ex.SqlState == "23505";
        }

        protected DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
        {
            // calculate absolute expiration
            DateTimeOffset? absoluteExpiration = null;
            if (options.AbsoluteExpirationRelativeToNow.HasValue)
            {
                absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }
            else if (options.AbsoluteExpiration.HasValue)
            {
                if (options.AbsoluteExpiration.Value <= utcNow)
                {
                    throw new InvalidOperationException("The absolute expiration value must be in the future.");
                }

                absoluteExpiration = options.AbsoluteExpiration.Value;
            }
            return absoluteExpiration;
        }

        protected void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
        {
            if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
            {
                throw new InvalidOperationException("Either absolute or sliding expiration needs to be provided.");
            }
        }
    }
}