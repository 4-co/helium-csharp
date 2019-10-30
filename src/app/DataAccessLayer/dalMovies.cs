using Helium.Model;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Helium.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB
    /// </summary>
    public partial class DAL
    {
        // select template for movies
        const string movieSelect = "select m.id, m.partitionKey, m.movieId, m.type, m.textSearch, m.title, m.year, m.runtime, m.rating, m.votes, m.totalScore, m.genres, m.roles from m where m.type = 'Movie' ";
        const string movieOrderBy = " order by m.title";

        /// <summary>
        /// Get a single movie by movieId
        /// 
        /// CosmosDB throws an exception if movieId not found
        /// </summary>
        /// <param name="movieId">movie ID to retrieve</param>
        /// <returns>Movie object</returns>
        public async Task<Movie> GetMovieAsync(string movieId)
        {
            // get the partition key for the movie ID
            // note: if the key cannot be determined from the ID, ReadDocumentAsync cannot be used.
            // GetPartitionKey will throw an ArgumentException if the movieId isn't valid
            // get a movie by ID
            return await container.ReadItemAsync<Movie>(movieId, new PartitionKey(GetPartitionKey(movieId)));
        }

        /// <summary>
        /// Get all movies
        /// </summary>
        /// <returns>List of all Movies</returns>
        public async Task<IEnumerable<Movie>> GetMoviesAsync()
        {
            // get all movies
            return await GetMoviesByQueryAsync(string.Empty);
        }

        /// <summary>
        /// Get a list of movies by searching the title
        /// </summary>
        /// <param name="q">search term</param>
        /// <param name="genre">get movies by genre</param>
        /// <param name="year">get movies by year</param>
        /// <param name="rating">get movies rated >= rating</param>
        /// <param name="toprated">get top rated movies</param>
        /// <param name="actorId">get movies by actorId</param>
        /// <returns>List of Movies or an empty list</returns>
        public async Task<IEnumerable<Movie>> GetMoviesByQueryAsync(string q, string genre = "", int year = 0, double rating = 0, bool toprated = false, string actorId = "")
        {
            string sql = movieSelect;
            string orderby = movieOrderBy;

            if (!string.IsNullOrEmpty(q))
            {
                // convert to lower and escape embedded '
                q = q.Trim().ToLower().Replace("'", "''");

                if (!string.IsNullOrEmpty(q))
                {
                    // get movies by a "like" search on title
                    sql += string.Format(" and contains(m.textSearch, '{0}') ", q);
                }
            }

            if (year > 0)
            {
                sql += string.Format(" and m.year = {0} ", year);
            }

            if (rating > 0)
            {
                sql += string.Format(" and m.rating >= {0} ", rating);
            }

            if (toprated)
            {
                sql = "select top 10 " + sql.Substring(7);
                orderby = " order by m.rating desc";
            }

            if (!string.IsNullOrEmpty(actorId))
            {
                // convert to lower and escape embedded '
                actorId = actorId.Trim().ToLower().Replace("'", "''");

                if (!string.IsNullOrEmpty(actorId))
                {
                    // get movies for an actor
                    sql += " and array_contains(m.roles, { actorId: '";
                    sql += actorId;
                    sql += "' }, true) ";
                }
            }

            if (!string.IsNullOrEmpty(genre))
            {
                // convert to lower and escape embedded '
                genre = await GetGenreAsync(genre);

                if (string.IsNullOrEmpty(genre))
                {
                    // genre doesn't exist
                    return new List<Movie>().AsQueryable<Movie>();
                }

                // get movies by genre
                sql += string.Format(" and array_contains(m.genres, '{0}') ", genre);
            }

            sql += orderby;

            return await QueryMovieWorkerAsync(sql);
        }

        /// <summary>
        /// Get the featured movie list from Cosmos
        /// </summary>
        /// <returns>List</returns>
        public async Task<List<string>> GetFeaturedMovieListAsync()
        {
            List<string> list = new List<string>();

            string sql = "select m.movieId, m.weight from m where m.type = 'Featured' order by m.weight desc";

            var query = container.GetItemQueryIterator<FeaturedMovie>(sql, requestOptions: queryRequestOptions);

            while (query.HasMoreResults)
            {
                foreach (var doc in await query.ReadNextAsync())
                {
                    // apply weighting
                    for (int i = 0; i < doc.weight; i++)
                    {
                        list.Add(doc.movieId);
                    }
                }
            }

            // default to The Matrix
            if (list.Count == 0)
            {
                list.Add("tt0133093");
            }

            return list;
        }

        /// <summary>
        /// Movie worker query
        /// </summary>
        /// <param name="sql">select statement to execute</param>
        /// <returns>List of Movies or empty list</returns>
        public async Task<IEnumerable<Movie>> QueryMovieWorkerAsync(string sql)
        {
            // run query
            var query = container.GetItemQueryIterator<Movie>(sql, requestOptions: queryRequestOptions);

            List<Movie> results = new List<Movie>();

            while (query.HasMoreResults)
            {
                foreach (var doc in await query.ReadNextAsync())
                {
                    results.Add(doc);
                }
            }
            return results;
        }
    }
}