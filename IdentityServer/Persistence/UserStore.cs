using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityModel;
using Microsoft.Extensions.Configuration;
using Npgsql;

// Global namespace

public class UserStore : IUserStore
{
    private string connectionString;
    public UserStore(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<bool> ValidateCredentials(string username, string password)
    {
        string hash = null;
        string salt = null;
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            DataTable table = new DataTable();
            using(var cmd = new NpgsqlCommand("SELECT PasswordSalt, PasswordHash FROM AppUser WHERE Username = @username;", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@username", username));
                var reader = await cmd.ExecuteReaderAsync();
                table.Load(reader);
                reader.Close();
            }
            if(table.Rows.Count > 0)
            {
                salt = (string)(table.Rows[0]["PasswordSalt"]);
                hash = (string)(table.Rows[0]["PasswordHash"]);
            }
        }
        return (String.IsNullOrEmpty(salt) || String.IsNullOrEmpty(hash)) ? false : AppUser.PasswordValidation(hash, salt, password);
    }

    public async Task<AppUser> FindBySubjectId(string subjectId)
    {
        AppUser user = null;
        using(NpgsqlCommand cmd = new NpgsqlCommand("SELECT * FROM AppUser WHERE SubjectId = @subjectid;"))
        {
            cmd.Parameters.Add(new NpgsqlParameter("@subjectid", subjectId));
            user = await ExecuteFindCommand(cmd);
        }
        return user;
    }

    public async Task<AppUser> FindByUsername(string username)
    {
        AppUser user = null;
        using(NpgsqlCommand cmd = new NpgsqlCommand("SELECT * FROM AppUser WHERE Username = @username;"))
        {
            cmd.Parameters.Add(new NpgsqlParameter("@username", username));
            user = await ExecuteFindCommand(cmd);
        }
        return user;
    }

    public async Task<AppUser> FindByExternalProvider(string provider, string subjectId)
    {
        AppUser user = null;
        using(NpgsqlCommand cmd = new NpgsqlCommand("SELECT * FROM AppUser WHERE ProviderName = @pname AND ProviderSubjectId = @psub;"))
        {
            cmd.Parameters.Add(new NpgsqlParameter("@pname", provider));
            cmd.Parameters.Add(new NpgsqlParameter("@psub", subjectId));
            user = await ExecuteFindCommand(cmd);
        }
        return user;
    }

    private async Task<AppUser> ExecuteFindCommand(NpgsqlCommand cmd)
    {
        AppUser user = null;
        using(var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            cmd.Connection = conn;
            var reader = await cmd.ExecuteReaderAsync();
            if(reader.HasRows)
            {
                DataTable table = new DataTable();
                table.Load(reader);
                await reader.CloseAsync();
                var userRow = table.Rows[0];
                user = new AppUser()
                {
                    id = (int)userRow["id"],
                    SubjectId = (string)userRow["SubjectId"],
                    Username = (string)userRow["Username"],
                    PasswordSalt = (string)userRow["PasswordSalt"],
                    PasswordHash = (string)userRow["PasswordHash"],
                    ProviderName = (string)userRow["ProviderName"],
                    ProviderSubjectId = (string)userRow["ProviderSubjectId"],
                };
                using(var claimcmd = new NpgsqlCommand("SELECT * FROM Claim WHERE AppUserId = @uid;", conn))
                {
                    claimcmd.Parameters.Add(new NpgsqlParameter("@uid", user.id));
                    var reader2 = await claimcmd.ExecuteReaderAsync();
                    if(reader2.HasRows)
                    {
                        table = new DataTable();
                        table.Load(reader2);
                        user.Claims = new List<Claim>(table.Rows.Count);
                        foreach(DataRow row in table.Rows)
                        {
                            user.Claims.Add(new Claim(
                                type: (string)row["Type"],
                                value: (string)row["Value"],
                                valueType: (string)row["ValueType"],
                                issuer: (string)row["Issuer"],
                                originalIssuer: (string)row["OriginalIssuer"]));
                        }
                    }
                    await reader2.CloseAsync();
                }
            }
            await reader.CloseAsync();
            cmd.Connection = null;
        }
        return user;
    }

    public async Task<AppUser> AutoProvisionUser(string provider, string subjectId, List<Claim> claims)
    {
        // create a list of claims that we want to transfer into our store
        var filtered = new List<Claim>();

        foreach(var claim in claims)
        {
            // if the external system sends a display name - translate that to the standard OIDC name claim
            if(claim.Type == ClaimTypes.Name)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, claim.Value));
            }
            // if the JWT handler has an outbound mapping to an OIDC claim use that
            else if(JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.ContainsKey(claim.Type))
            {
                filtered.Add(new Claim(JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap[claim.Type], claim.Value));
            }
            // copy the claim as-is
            else
            {
                filtered.Add(claim);
            }
        }

        // if no display name was provided, try to construct by first and/or last name
        if(!filtered.Any(x => x.Type == JwtClaimTypes.Name))
        {
            var first = filtered.FirstOrDefault(x => x.Type == JwtClaimTypes.GivenName)?.Value;
            var last = filtered.FirstOrDefault(x => x.Type == JwtClaimTypes.FamilyName)?.Value;
            if(first != null && last != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, first + " " + last));
            }
            else if(first != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, first));
            }
            else if(last != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, last));
            }
        }

        // create a new unique subject id
        var sub = CryptoRandom.CreateUniqueId();

        // check if a display name is available, otherwise fallback to subject id
        var name = filtered.FirstOrDefault(c => c.Type == JwtClaimTypes.Name)?.Value ?? sub;

        // create new user
        var user = new AppUser
        {
            SubjectId = sub,
            Username = name,
            ProviderName = provider,
            ProviderSubjectId = subjectId,
            Claims = filtered
        };

        // store it and give it back
        await SaveAppUser(user);
        return user;
    }

    public async Task<bool> SaveAppUser(AppUser user, string newPasswordToHash = null)
    {
        bool success = true;
        if(!String.IsNullOrEmpty(newPasswordToHash))
        {
            user.PasswordSalt = AppUser.PasswordSaltInBase64();
            user.PasswordHash = AppUser.PasswordToHashBase64(newPasswordToHash, user.PasswordSalt);
        }
        try
        {
            using(var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string upsert =
@$"WITH
s AS (
    SELECT @userid AS Id
),
upd AS(
    UPDATE AppUser
    SET SubjectId=@subjectid, Username=@username, PasswordHash=@passwordhash, PasswordSalt=@passwordsalt, ProviderName=@providername, ProviderSubjectId=@providersubjectid
    FROM s
    WHERE AppUser.Id = s.Id
    RETURNING AppUser.Id
)
INSERT INTO AppUser(Id,SubjectId,Username,PasswordHash,PasswordSalt,ProviderName,ProviderSubjectId)
SELECT NEXTVAL('appuser_id_seq'),@subjectid,@username,@passwordhash,@passwordsalt,@providername,@providersubjectid
FROM   s
WHERE  s.Id NOT IN (SELECT Id FROM upd)
RETURNING Id";

                object result = null;
                using(var cmd = new NpgsqlCommand(upsert, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@userid", user.id));
                    cmd.Parameters.Add(new NpgsqlParameter("@subjectid", user.SubjectId));
                    cmd.Parameters.Add(new NpgsqlParameter("@username", user.Username));
                    cmd.Parameters.Add(new NpgsqlParameter("@passwordhash", user.PasswordHash));
                    cmd.Parameters.Add(new NpgsqlParameter("@passwordsalt", user.PasswordSalt));
                    cmd.Parameters.Add(new NpgsqlParameter("@providername", user.ProviderName));
                    cmd.Parameters.Add(new NpgsqlParameter("@providersubjectid", user.ProviderSubjectId));
                    result = await cmd.ExecuteScalarAsync();
                }
                int newId = (result is null || result is DBNull) ? user.id : Convert.ToInt32(result); // null on update, value on insert
                if(newId > 0) user.id = newId;
                if(user.id > 0 && user.Claims.Count > 0)
                {
                    string insertIfNew =
@$"WITH
s AS (
    SELECT @userid AS uid, @subjectid AS sub, @type AS type, @value AS val
),
upd AS(
    UPDATE Claim
    SET AppUserId=@userid,Issuer=@issuer,OriginalIssuer=@origissuer,Subject=@subjectid,Type=@type,Value=@value,ValueType=@valuetype
    FROM s
    WHERE Claim.AppUserId=s.uid AND Claim.Subject=s.sub AND Claim.Type=s.type 
    RETURNING Claim.AppUserId,Claim.Subject,Claim.Type
)
INSERT INTO Claim(Id,AppUserId,Issuer,OriginalIssuer,Subject,Type,Value,ValueType)
SELECT NEXTVAL('claim_id_seq'),@userid,@issuer,@origissuer,@subjectid,@type,@value,@valuetype
FROM   s
WHERE  (s.uid,s.sub,s.type) NOT IN (SELECT AppUserId,Subject,Type FROM upd)";
                    foreach (Claim c in user.Claims)
                    {
                        using(var cmd = new NpgsqlCommand(insertIfNew, conn))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@userid", user.id));
                            cmd.Parameters.Add(new NpgsqlParameter("@issuer", c.Issuer ?? string.Empty));
                            cmd.Parameters.Add(new NpgsqlParameter("@origissuer", c.OriginalIssuer ?? string.Empty));
                            cmd.Parameters.Add(new NpgsqlParameter("@subjectid", user.SubjectId));
                            cmd.Parameters.Add(new NpgsqlParameter("@type", c.Type));
                            cmd.Parameters.Add(new NpgsqlParameter("@value", c.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@valuetype", c.ValueType ?? string.Empty));
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }
        catch(Exception e)
        {
            success = false;
            Debug.WriteLine(e);
        }
        return success;
    }
}