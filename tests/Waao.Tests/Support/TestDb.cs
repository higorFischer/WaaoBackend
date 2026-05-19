using Microsoft.EntityFrameworkCore;
using Waao.Infra.EF;

namespace Waao.Tests.Support;

public static class TestDb
{
	public static WaaoDbContext New()
	{
		var options = new DbContextOptionsBuilder<WaaoDbContext>()
			.UseInMemoryDatabase($"waao-{Guid.NewGuid()}")
			.Options;
		return new WaaoDbContext(options);
	}
}
