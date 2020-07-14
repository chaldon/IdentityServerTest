using IdentityServer4.Models;
using IdentityServer4.Stores;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using Npgsql;
using System.Threading.Tasks;

// Global namespace

public class PersistedGrantStore : IPersistedGrantStore
{
    private string connectionString;
    public PersistedGrantStore(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    private async Task<IEnumerable<PersistedGrant>> GetAllAsync(string subjectId)
    {
        var grants = new List<PersistedGrant>();
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using(var cmd = new NpgsqlCommand("SELECT * FROM \"grant\" WHERE SubjectId = @sub;", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@sub", subjectId));
                var reader = await cmd.ExecuteReaderAsync();
                if(reader.HasRows)
                {
                    var table = new DataTable();
                    table.Load(reader);
                    foreach(DataRow row in table.Rows)
                    {
                        grants.Add(DataToGrant(row));
                    }
                }
                reader.Close();
            }
        }
        return grants;
    }

    public async Task<PersistedGrant> GetAsync(string key)
    {
        PersistedGrant grant = null;
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using(var cmd = new NpgsqlCommand("SELECT * FROM \"grant\" WHERE Key = @key", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@key", key));
                var reader = await cmd.ExecuteReaderAsync();
                if(reader.HasRows)
                {
                    var table = new DataTable();
                    table.Load(reader);
                    grant = DataToGrant(table.Rows[0]);
                }
                reader.Close();
            }
        }
        return grant;
    }

    private async Task RemoveAllAsync(string subjectId, string clientId)
    {
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using(var cmd = new NpgsqlCommand("DELETE FROM \"grant\" WHERE SubjectId = @sub AND ClientId = @client", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@sub", subjectId));
                cmd.Parameters.Add(new NpgsqlParameter("@client", clientId));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task RemoveAllAsync(string subjectId, string clientId, string type)
    {
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using(var cmd = new NpgsqlCommand("DELETE FROM \"grant\" WHERE SubjectId = @sub AND ClientId = @client AND Type = @type", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@sub", subjectId));
                cmd.Parameters.Add(new NpgsqlParameter("@client", clientId));
                cmd.Parameters.Add(new NpgsqlParameter("@type", type));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task RemoveAsync(string key)
    {
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            using(var cmd = new NpgsqlCommand("DELETE FROM \"grant\" WHERE Key = @key", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@key", key));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task StoreAsync(PersistedGrant grant)
    {
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            string upsert =
@$"WITH
s AS (
    SELECT @key AS Key
),
upd AS(
    UPDATE ""grant""
    SET ClientId=@clientid,CreationTime=@creationtime,Data=@data,Expiration=@expiration,SubjectId=@subjectid,Type=@type 
    FROM s
    WHERE ""grant"".Key = s.Key
    RETURNING ""grant"".Key
)
INSERT INTO ""grant""(Id,Key,ClientId,CreationTime,Data,Expiration,SubjectId,Type)
SELECT NEXTVAL('grant_id_seq'),@key,@clientid,@creationtime,@data,@expiration,@subjectid,@type
FROM   s
WHERE  s.Key NOT IN (SELECT Key FROM upd)";
            using(var cmd = new NpgsqlCommand(upsert, conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@key", grant.Key));
                cmd.Parameters.Add(new NpgsqlParameter("@clientid", grant.ClientId));
                cmd.Parameters.Add(new NpgsqlParameter("@creationtime", grant.CreationTime));
                cmd.Parameters.Add(new NpgsqlParameter("@data", grant.Data));
                cmd.Parameters.Add(new NpgsqlParameter("@expiration", grant.Expiration));
                cmd.Parameters.Add(new NpgsqlParameter("@subjectid", grant.SubjectId));
                cmd.Parameters.Add(new NpgsqlParameter("@type", grant.Type));
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private string NullOrDate(DateTime? value)
    {
        return (value.HasValue) ? $"'{FormatDate(value.Value)}'" : "null";
    }

    private string FormatDate(DateTime value)
    {
        // UTC ISO 8601 format
        return ((DateTimeOffset)value).ToUniversalTime().ToString("o");
    }

    private PersistedGrant DataToGrant(DataRow row)
    {
        DateTime? expiration = (row["Expiration"] is DBNull) ? null : (DateTime?)row["Expiration"];
        return new PersistedGrant()
        {
            Key = (string)row["Key"],
            ClientId = (string)row["ClientId"],
            CreationTime = (DateTime)row["CreationTime"],
            Data = (string)row["Data"],
            Expiration = expiration,
            SubjectId = (string)row["SubjectId"],
            Type = (string)row["Type"]
        };
    }

    public async Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
    {
        return filter.SubjectId != null ? await GetAllAsync(filter.SubjectId) : null;
    }

    public async Task RemoveAllAsync(PersistedGrantFilter filter)
    {
        if (filter.SubjectId != null) await RemoveAllAsync(filter.SubjectId, filter.SubjectId);
    }
}