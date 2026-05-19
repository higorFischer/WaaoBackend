using FluentAssertions;
using Xunit;
using Waao.Tests.Support;

namespace Waao.Tests;

public class SanityTests
{
	[Fact]
	public void InMemoryContext_CanBeCreated()
	{
		using var db = TestDb.New();
		db.Should().NotBeNull();
	}
}
