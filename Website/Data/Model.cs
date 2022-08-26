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

	public WebsiteContext(DbContextOptions<WebsiteContext> options) : base(options) { }
}
#nullable enable

[Table("Users")]
public class User
{
	[Key]
	public int Vid { get; set; }
	public DateTime LastControlTime { get; set; }
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