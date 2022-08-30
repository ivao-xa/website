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
	public DbSet<DeviationReport> Deviations { get; set; }

	public WebsiteContext(DbContextOptions<WebsiteContext> options) : base(options) { }
}
#nullable enable

[Table("Users")]
public class User
{
	[Key]
	public int Vid { get; set; }
	public ulong? Snowflake { get; set; }
	public DiscordRoles Roles { get; set; }
	public DateTime LastControlTime { get; set; } = DateTime.MinValue;
	public DateTime LastPilotTime { get; set; } = DateTime.MinValue;

	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public AtcRating? RatingAtc { get; set; }
	public PilotRating? RatingPilot { get; set; }
	public string? Division { get; set; }
	public string? Country { get; set; }
	public string? Staff { get; set; }

	[NotMapped]
	public string Mention => Snowflake is ulong l ? $"<@{l}>" : Vid.ToString("000000");
}

[Flags]
public enum DiscordRoles : ulong
{
	Member	= 0b01,
	Staff	= 0b10,

	Controller	= 0b01_00,
	Pilot		= 0b10_00,

	Training	= 0b0001_00_00_00,
	Membership	= 0b0010_00_00_00,

	Administrator = 0x8_00000_0000_00_00_00L
}

public enum AtcRating : int
{
	AS1 = 2,
	AS2 = 3,
	AS3 = 4,
	ADC = 5,
	APC = 6,
	ACC = 7,
	SEC = 8,
	SAI = 9,
	CAI = 10
}

public enum PilotRating : int
{
	FS1 = 2,
	FS2 = 3,
	FS3 = 4,
	PP = 5,
	SPP = 6,
	CP = 7,
	ATP = 8,
	SFI = 9,
	CFI = 10
}

[Table("Deviations")]
public class DeviationReport
{
	public int Id { get; set; }

	public int Reporter { get; set; }
	public int Reportee { get; set; }
	public string? Callsign { get; set; }
	public string Body { get; set; } = string.Empty;
	public DateTime FilingTime { get; set; } = DateTime.UtcNow;
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