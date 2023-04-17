using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using MySql.EntityFrameworkCore.Extensions;

using Org.BouncyCastle.Security;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Website.Data;

#nullable disable
public class WebsiteContext : DbContext
{
	public DbSet<User> Users { get; set; }
	public DbSet<DeviationReport> Deviations { get; set; }
	public DbSet<Document> Documents { get; set; }
	public DbSet<Event> Events { get; set; }
	public DbSet<EventSignup> EventSignups { get; set; }
	public DbSet<Exam> Exams { get; set; }
	public DbSet<TrainingRequest> TrainingRequests { get; set; }

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

	public string? Nickname { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public AtcRating? RatingAtc { get; set; }
	public PilotRating? RatingPilot { get; set; }
	public string? Division { get; set; }
	public string? Country { get; set; }
	public string? Staff { get; set; }

	public bool FaaChecked { get; set; } = true;
	public bool NavCanChecked { get; set; } = false;

	[NotMapped]
	public string Mention => Snowflake is ulong l ? $"<@{l}>" : Vid.ToString("000000");

	[NotMapped]
	public string Name => Nickname ?? FirstName ?? "";

	public override string ToString() => $"{Vid} ({Nickname ?? FirstName} {LastName})";
}

[Flags]
public enum DiscordRoles : ulong
{
	Member = 0b01,
	Staff = 0b10,

	Controller = 0b01_00,
	Pilot = 0b10_00,

	Announcement = 0b01_00_00,

	Training = 0b0001_00_00_00,
	Membership = 0b0010_00_00_00,
	Events = 0b0100_00_00_00,

	Administrator = 0x8_00000_0000_00_00_00L,

	All = 0x8_00000_0111_01_11_11L
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

[Table("Documents")]
public class Document
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;
	public string Path { get; set; } = string.Empty;
	public string Positions { get; set; } = string.Empty;
	public string Departments { get; set; } = string.Empty;
}

[Table("Events")]
public class Event : ICalendarItem
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;
	public DateTime Start { get; set; } = DateTime.UtcNow;
	public DateTime End { get; set; } = DateTime.UtcNow;
	public string InternalPositions { get; set; } = string.Empty;
	[NotMapped]
	public string[] Positions
	{
		get => InternalPositions.Split(); 
		set => InternalPositions = string.Join(' ', value); 
	}
	public string Controllers { get; set; } = string.Empty;

	public string ForumUrl { get; set; } = string.Empty;
	public string? BannerUrl { get; set; } = null;

	public IQueryable<EventSignup> Signups(WebsiteContext context) => context.EventSignups.Where(su => su.EventId == Id);
	public void AddOrUpdateSignup(WebsiteContext context, EventSignup signup)
	{
		if (context.EventSignups.Any(su => su.Controller == signup.Controller && su.Position == signup.Position && su.Time == signup.Time))
			context.EventSignups.RemoveRange(context.EventSignups.Where(su => su.Controller == signup.Controller && su.Position == signup.Position && su.Time == signup.Time));

		signup = new() {
			EventId = Id,
			Controller = signup.Controller,
			Position = signup.Position,
			Time = signup.Time
		};

		context.EventSignups.Add(signup);
	}

	public void RemoveSignup(WebsiteContext context, EventSignup signup)
	{
		if (context.EventSignups.Any(su => su.Controller == signup.Controller && su.Position == signup.Position && su.Time == signup.Time))
			context.EventSignups.RemoveRange(context.EventSignups.Where(su => su.Controller == signup.Controller && su.Position == signup.Position && su.Time == signup.Time));
	}
}

[Table("EventSignups")]
public class EventSignup
{
	public int Id { get; set; }
	[ForeignKey("Events.Id")]
	public int EventId { get; set; }

	public int Controller { get; set; }
	public string Position { get; set; } = "";
	public DateTime Time { get; set; }
}

[Table("Exams")]
public class Exam : ICalendarItem
{
	public int Id { get; set; }

	public AtcRating Rating { get; set; }
	public bool Mock { get; set; } = false;
	public int Trainee { get; set; }
	public int Trainer { get; set; }
	public string Position { get; set; } = string.Empty;
	public string? BannerUrl { get; set; } = null;
	public DateTime Start { get; set; } = DateTime.UtcNow;

	public DateTime End => Start + TimeSpan.FromHours(2);
	public string Name => $"{Rating} {(Mock ? "training" : "exam")} at {Position}";
}

[Table("TrainingRequests")]
public class TrainingRequest
{
	public int Id { get; set; }

	public AtcRating? AtcRating { get; set; }
	public PilotRating? PilotRating { get; set; }
	public int Trainee { get; set; }
	public int? Trainer { get; set; }
	public string Position { get; set; } = string.Empty;
	public string Comments { get; set; } = string.Empty;
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

public interface ICalendarItem
{
	public DateTime Start { get; }
	public DateTime End { get; }
	public string Name { get; }
}