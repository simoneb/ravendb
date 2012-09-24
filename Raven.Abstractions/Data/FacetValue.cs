using System.Collections.Generic;

namespace Raven.Abstractions.Data
{
	public class FacetValue
	{
		public string Range { get; set; }
		public int Count { get; set; }
		public IDictionary<string, IEnumerable<FacetValue>> Children { get; set; }

		public FacetValue()
		{
			Children = new Dictionary<string, IEnumerable<FacetValue>>();
		}
	}
}
