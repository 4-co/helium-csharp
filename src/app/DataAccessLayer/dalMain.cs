using Microsoft.Azure.Cosmos;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Helium.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB
    /// </summary>
    public partial class DAL : IDAL
    {
        public int DefaultPageSize = 100;
        public int MaxPageSize = 1000;
        public int CosmosTimeout = 60;
        public int CosmosMaxRetries = 10;

        private CosmosDetails _cosmosDetails = null;


        /// <summary>
        /// Data Access Layer Constructor
        /// </summary>
        /// <param name="cosmosUrl">CosmosDB Url</param>
        /// <param name="cosmosKey">CosmosDB connection key</param>
        /// <param name="cosmosDatabase">CosmosDB Database</param>
        /// <param name="cosmosCollection">CosmosDB Collection</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "objects are used by DI")]
        public DAL(Uri cosmosUrl, string cosmosKey, string cosmosDatabase, string cosmosCollection)
        {
            if (cosmosUrl == null)
            {
                throw new ArgumentNullException(nameof(cosmosUrl));
            }

            _cosmosDetails = new CosmosDetails
            {
                MaxRows = MaxPageSize,
                Retries = CosmosMaxRetries,
                Timeout = CosmosTimeout,
                CosmosCollection = cosmosCollection,
                CosmosDatabase = cosmosDatabase,
                CosmosKey = cosmosKey,
                CosmosUrl = cosmosUrl.AbsoluteUri
            };

            // create the CosmosDB client and container
            _cosmosDetails.Client = OpenAndTestCosmosClient(cosmosUrl, cosmosKey, cosmosDatabase, cosmosCollection).GetAwaiter().GetResult();
            _cosmosDetails.Container = _cosmosDetails.Client.GetContainer(cosmosDatabase, cosmosCollection);
        }

        /// <summary>
        /// Worker method that executes a query
        /// </summary>
        /// <param name="sql">the select statement to execute</param>
        /// <returns>results of the query</returns>
        public FeedIterator<dynamic> QueryWorker(string sql)
        {
            return _cosmosDetails.Container.GetItemQueryIterator<dynamic>(sql, requestOptions: _cosmosDetails.QueryRequestOptions);
        }

        /// <summary>
        /// Recreate the Cosmos Client / Container (after a key rotation)
        /// </summary>
        /// <param name="cosmosUrl">Cosmos URL</param>
        /// <param name="cosmosKey">Cosmos Key</param>
        /// <param name="cosmosDatabase">Cosmos Database</param>
        /// <param name="cosmosCollection">Cosmos Collection</param>
        /// <param name="force">force reconnection even if no params changed</param>
        /// <returns>Task</returns>
        public async Task Reconnect(Uri cosmosUrl, string cosmosKey, string cosmosDatabase, string cosmosCollection, bool force = false)
        {
            if (cosmosUrl == null)
            {
                throw new ArgumentNullException(nameof(cosmosUrl));
            }

            if (force ||
                _cosmosDetails.CosmosCollection != cosmosCollection ||
                _cosmosDetails.CosmosDatabase != cosmosDatabase ||
                _cosmosDetails.CosmosKey != cosmosKey ||
                _cosmosDetails.CosmosUrl != cosmosUrl.AbsoluteUri)
            {
                CosmosDetails d = new CosmosDetails
                {
                    CosmosCollection = cosmosCollection,
                    CosmosDatabase = cosmosDatabase,
                    CosmosKey = cosmosKey,
                    CosmosUrl = cosmosUrl.AbsoluteUri
                };

                // open and test a new client / container
                d.Client = await OpenAndTestCosmosClient(cosmosUrl, cosmosKey, cosmosDatabase, cosmosCollection).ConfigureAwait(false);
                d.Container = d.Client.GetContainer(cosmosDatabase, cosmosCollection);

                // set the current CosmosDetail
                _cosmosDetails = d;
            }
        }

        /// <summary>
        /// Open and test the Cosmos Client / Container / Query
        /// </summary>
        /// <param name="cosmosUrl">Cosmos URL</param>
        /// <param name="cosmosKey">Cosmos Key</param>
        /// <param name="cosmosDatabase">Cosmos Database</param>
        /// <param name="cosmosCollection">Cosmos Collection</param>
        /// <returns>An open and validated CosmosClient</returns>
        private async Task<CosmosClient> OpenAndTestCosmosClient(Uri cosmosUrl, string cosmosKey, string cosmosDatabase, string cosmosCollection)
        {
            // validate required parameters
            if (cosmosUrl == null)
            {
                throw new ArgumentNullException(nameof(cosmosUrl));
            }

            if (string.IsNullOrEmpty(cosmosKey))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, $"CosmosKey not set correctly {cosmosKey}"));
            }

            if (string.IsNullOrEmpty(cosmosDatabase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, $"CosmosDatabase not set correctly {cosmosDatabase}"));
            }

            if (string.IsNullOrEmpty(cosmosCollection))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, $"CosmosCollection not set correctly {cosmosCollection}"));
            }

            // open and test a new client / container
            var c = new CosmosClient(cosmosUrl.AbsoluteUri, cosmosKey, _cosmosDetails.CosmosClientOptions);
            var con = c.GetContainer(cosmosDatabase, cosmosCollection);
            await con.ReadItemAsync<dynamic>("action", new PartitionKey("0")).ConfigureAwait(false);

            return c;
        }


        /// <summary>
        /// Compute the partition key based on the movieId or actorId
        /// 
        /// For this sample, the partitionkey is the id mod 10
        /// 
        /// In a full implementation, you would update the logic to determine the partition key
        /// </summary>
        /// <param name="id">document id</param>
        /// <returns>the partition key</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "log message")]
        public static string GetPartitionKey(string id)
        {
            // validate id
            if (!string.IsNullOrEmpty(id) &&
                id.Length > 5 &&
                (id.StartsWith("tt", StringComparison.OrdinalIgnoreCase) || id.StartsWith("nm", StringComparison.OrdinalIgnoreCase)) &&
                int.TryParse(id.Substring(2), out int idInt))
            {
                return (idInt % 10).ToString(CultureInfo.InvariantCulture);
            }

            throw new ArgumentException("Invalid Partition Key");
        }
    }

    internal class CosmosDetails
    {
        public CosmosClient Client = null;
        public Container Container = null;

        // default values for Cosmos Options
        public int MaxRows = 1000;
        public int Timeout = 60;
        public int Retries = 10;

        // Cosmos connection fields
        public string CosmosUrl;
        public string CosmosKey;
        public string CosmosDatabase;
        public string CosmosCollection;

        // member variables
        private QueryRequestOptions _queryRequestOptions = null;
        private CosmosClientOptions _cosmosClientOptions = null;

        // CosmosDB query request options
        public QueryRequestOptions QueryRequestOptions
        {
            get
            {
                if (_queryRequestOptions == null)
                {
                    _queryRequestOptions = new QueryRequestOptions { MaxItemCount = MaxRows };
                }

                return _queryRequestOptions;
            }
        }

        // default protocol is tcp, default connection mode is direct
        public CosmosClientOptions CosmosClientOptions
        {
            get
            {
                if (_cosmosClientOptions == null)
                {
                    _cosmosClientOptions = new CosmosClientOptions { RequestTimeout = TimeSpan.FromSeconds(Timeout), MaxRetryAttemptsOnRateLimitedRequests = Retries, MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(Timeout) };
                }

                return _cosmosClientOptions;
            }
        }
    }
}