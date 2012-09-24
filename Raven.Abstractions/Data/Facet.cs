using System.Collections.Generic;

namespace Raven.Abstractions.Data
{
	public class Facet
	{
		public FacetMode Mode { get; set; }
		public string Name { get; set; }
		public List<string> Ranges { get; set; }
		public List<Facet> Children { get; set; }

		public Facet()
		{
			Ranges = new List<string>();
			Children = new List<Facet>();
		}
	}
}