using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Database.Extensions;

namespace Raven.Database.Queries
{
	public class FacetedQueryRunner
	{
		private readonly DocumentDatabase database;

		public FacetedQueryRunner(DocumentDatabase database)
		{
			this.database = database;
		}

		public IDictionary<string, IEnumerable<FacetValue>> GetFacets(string index, IndexQuery indexQuery, string facetSetupDoc)
		{
			var facetSetup = database.Get(facetSetupDoc, null);
			if (facetSetup == null)
				throw new InvalidOperationException("Could not find facets document: " + facetSetupDoc);

			var facets = facetSetup.DataAsJson.JsonDeserialization<FacetSetup>().Facets;

			var results = new Dictionary<string, IEnumerable<FacetValue>>();

			IndexSearcher currentIndexSearcher;
			using (database.IndexStorage.GetCurrentIndexSearcher(index, out currentIndexSearcher))
			{
				DoGetFacets(index, indexQuery, facets, currentIndexSearcher, results);
			}

			return results;
		}

		private void DoGetFacets(string index, IndexQuery indexQuery, List<Facet> facets, IndexSearcher currentIndexSearcher, IDictionary<string, IEnumerable<FacetValue>> results)
		{
			foreach (var facet in facets)
			{
				switch (facet.Mode)
				{
					case FacetMode.Default:
						HandleTermsFacet(index, facet, indexQuery, currentIndexSearcher, results);
						HandleChildren(index, indexQuery, currentIndexSearcher, results, facet);
						break;
					case FacetMode.Ranges:
						HandleRangeFacet(index, facet, indexQuery, currentIndexSearcher, results);
						HandleChildren(index, indexQuery, currentIndexSearcher, results, facet);
						break;
					default:
						throw new ArgumentException(string.Format("Could not understand '{0}'", facet.Mode));
				}
			}
		}

		private void HandleChildren(string index, IndexQuery indexQuery, IndexSearcher currentIndexSearcher, IDictionary<string, IEnumerable<FacetValue>> results,
		                            Facet facet)
		{
			foreach (var result in results[facet.Name])
			{
				var childQuery = indexQuery.Clone();
				childQuery.Query += " AND " + facet.Name + ":" + result.Range;
				DoGetFacets(index, childQuery, facet.Children, currentIndexSearcher, result.Children);
			}
		}

		private void HandleRangeFacet(string index, Facet facet, IndexQuery indexQuery, IndexSearcher currentIndexSearcher, IDictionary<string, IEnumerable<FacetValue>> results)
		{
			var rangeResults = new List<FacetValue>();
			foreach (var range in facet.Ranges)
			{
				var baseQuery = database.IndexStorage.GetLuceneQuery(index, indexQuery, database.IndexQueryTriggers);
				//TODO the built-in parser can't handle [NULL TO 100.0}, i.e. a mix of [ and }
				//so we need to handle this ourselves (greater and less-than-or-equal)
				var rangeQuery = database.IndexStorage.GetLuceneQuery(index, new IndexQuery
				{
					Query = facet.Name + ":" + range
				}, database.IndexQueryTriggers);

				var joinedQuery = new BooleanQuery();
				joinedQuery.Add(baseQuery, BooleanClause.Occur.MUST);
				joinedQuery.Add(rangeQuery, BooleanClause.Occur.MUST);

				var topDocs = currentIndexSearcher.Search(joinedQuery, null, 1);

				if (topDocs.TotalHits > 0)
				{
					rangeResults.Add(new FacetValue
					{
						Count = topDocs.TotalHits,
						Range = range
					});
				}
			}

			results[facet.Name] = rangeResults;
		}

		private void HandleTermsFacet(string index, Facet facet, IndexQuery indexQuery, IndexSearcher currentIndexSearcher, IDictionary<string, IEnumerable<FacetValue>> results)
		{
			var terms = database.ExecuteGetTermsQuery(index,
													  facet.Name, null,
													  database.Configuration.MaxPageSize);
			var termResults = new List<FacetValue>();
			var baseQuery = database.IndexStorage.GetLuceneQuery(index, indexQuery, database.IndexQueryTriggers);
			foreach (var term in terms)
			{
				var termQuery = new TermQuery(new Term(facet.Name, term));

				var joinedQuery = new BooleanQuery();
				joinedQuery.Add(baseQuery, BooleanClause.Occur.MUST);
				joinedQuery.Add(termQuery, BooleanClause.Occur.MUST);

				var topDocs = currentIndexSearcher.Search(joinedQuery, null, 1);

				if (topDocs.TotalHits > 0)
				{
					termResults.Add(new FacetValue
					{
						Count = topDocs.TotalHits,
						Range = term
					});
				}
			}

			results[facet.Name] = termResults;
		}
	}
}