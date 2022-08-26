using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using MySql.EntityFrameworkCore.Extensions;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website.Data;

#nullable disable
public class WebsiteContext : DbContext
{
	public DbSet<User> Users { get; set; }
	public DbSet<UserRole> UserRoles { get; set; }

	public WebsiteContext(DbContextOptions<WebsiteContext> options) : base(options) { }
}
#nullable enable

[Table("Users")]
public class User
{
	[Key]
	public int Vid { get; set; }
	public UserRole? Discord { get; set; }
	public DateTime LastControlTime { get; set; } = DateTime.MinValue;
	public DateTime LastPilotTime { get; set; } = DateTime.MinValue;

	[NotMapped]
	public string Mention => Discord?.Snowflake is ulong l ? $"<@{l}>" : Vid.ToString("000000");
}

[Table("Roles")]
public class UserRole
{
	[Key]
	public ulong Snowflake { get; set; }

	public DiscordRoles Roles { get; set; }
}

[Flags]
public enum DiscordRoles : ulong
{
	Member	= 0b01,
	Staff	= 0b10,

	Controller	= 0b01_00,
	Pilot		= 0b10_00,

	Administrator = 0x8000_0000_0000_0000L
}

// https://www.svrz.com/unable-to-resolve-service-for-type-microsoft-entityframeworkcore-storage-typemappingsourcedependencies/
public class MysqlEntityFrameworkDesignTimeServices : IDesignTimeServices
{
	public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
	{
		serviceCollection.AddEntityFrameworkMySQL();
		new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection).TryAddCoreServices();
	}
}