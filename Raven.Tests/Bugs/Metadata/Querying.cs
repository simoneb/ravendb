using System.Linq;
using Xunit;

namespace Raven.Tests.Bugs.Metadata
{
	public class Querying : LocalClientTest
	{
		public void Can_query_metadata()
		{
			using(var DocStore = NewDocumentStore())
			{
				var user1 = new User { Name = "Joe Schmoe" };
				// This test succeeds if I use "Test-Property1" as the  property name.
				const string propertyName1 = "Test-Property-1";
				const string propertyValue1 = "Test-Value-1";
				using (var session = DocStore.OpenSession())
				{
					session.Store(user1);
					var metadata1 = session.Advanced.GetMetadataFor(user1);
					metadata1[propertyName1] = propertyValue1;
					session.Store(new User { Name = "Ralph Schmoe" });
					session.SaveChanges();
				}

				using (var session = DocStore.OpenSession())
				{
					var result = session.Advanced.LuceneQuery<User>()
						.WaitForNonStaleResultsAsOfNow()
						.WhereEquals("@metadata." + propertyName1, propertyValue1)
						.ToList();

					Assert.NotNull(result);
					Assert.Equal(1, result.Count);
					var metadata = session.Advanced.GetMetadataFor(result[0]);
					Assert.Equal(propertyValue1, metadata[propertyName1]);
				}
			}
		}
	}
}