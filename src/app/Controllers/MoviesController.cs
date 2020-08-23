﻿using System;
using CSE.Helium.DataAccessLayer;
using CSE.Helium.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Threading.Tasks;
using CSE.Helium.Model;
using CSE.Helium.Validation;

namespace CSE.Helium.Controllers
{
    /// <summary>
    /// Handle all of the /api/movies requests
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MoviesController : Controller
    {
        private readonly ILogger logger;
        private readonly IDAL dal;
        private readonly IParameterValidator parameterValidator;

        /// <summary>
        ///  Constructor
        /// </summary>
        /// <param name="logger">log instance</param>
        /// <param name="dal">data access layer instance</param>
        public MoviesController(ILogger<MoviesController> logger, IDAL dal, IParameterValidator parameterValidator)
        {
            this.logger = logger;
            this.dal = dal;
            this.parameterValidator = parameterValidator;
        }

        /// <summary>
        /// Returns a JSON array of Movie objects
        /// </summary>
        /// <param name="movieQueryParameters"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetMoviesAsync([FromQuery]MovieQueryParameters movieQueryParameters)

        {
            _ = movieQueryParameters ?? throw new ArgumentNullException(nameof(movieQueryParameters));

            string method = GetMethodText(
                movieQueryParameters.Q,
                movieQueryParameters.Genre,
                movieQueryParameters.Year,
                movieQueryParameters.Rating,
                movieQueryParameters.ActorId,
                movieQueryParameters.PageNumber,
                movieQueryParameters.PageSize);

            // convert to zero based page index
            movieQueryParameters.PageNumber = movieQueryParameters.PageNumber > 1 ? movieQueryParameters.PageNumber - 1 : 0;

            return await ResultHandler.Handle(dal.GetMoviesAsync(
                        movieQueryParameters.Q,
                        movieQueryParameters.Genre,
                        movieQueryParameters.Year,
                        movieQueryParameters.Rating,
                        movieQueryParameters.ActorId,
                        movieQueryParameters.PageNumber * movieQueryParameters.PageSize,
                        movieQueryParameters.PageSize),
                    method,
                    Constants.MoviesControllerException,
                    logger)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a single JSON Movie by movieId
        /// </summary>
        /// <param name="movieId">The movieId</param>
        /// <response code="404">movieId not found</response>
        [HttpGet("{movieId}")]
        public async System.Threading.Tasks.Task<IActionResult> GetMovieByIdAsync(string movieId)
        {
            string method = "GetMovieByIdAsync " + movieId;

            // validate movieId
            var result = parameterValidator.ValidateMovieId(movieId, method, logger);
            if (result != null)
            {
                return result;
            }

            // get movie by movieId
            return await ResultHandler.Handle(dal.GetMovieAsync(movieId), method, "Movie Not Found", logger).ConfigureAwait(false);
        }

        /// <summary>
        /// Add parameters to the method name if specified in the query string
        /// </summary>
        /// <param name="q"></param>
        /// <param name="genre"></param>
        /// <param name="year"></param>
        /// <param name="rating"></param>
        /// <param name="actorId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private string GetMethodText(string q, string genre, int year, double rating, string actorId, int pageNumber, int pageSize)
        {
            string method = "GetMovies";

            if (HttpContext != null && HttpContext.Request != null && HttpContext.Request.Query != null)
            {
                // add the query parameters to the method name if exists
                if (HttpContext.Request.Query.ContainsKey("q"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:q:{q}");
                }

                if (HttpContext.Request.Query.ContainsKey("genre"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:genre:{genre}");
                }

                if (HttpContext.Request.Query.ContainsKey("year"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:year:{year}");
                }

                if (HttpContext.Request.Query.ContainsKey("rating"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:rating:{rating}");
                }

                if (HttpContext.Request.Query.ContainsKey("actorId"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:actorId:{actorId}");
                }

                if (HttpContext.Request.Query.ContainsKey("pageNumber"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:pageNumber:{pageNumber}");
                }

                if (HttpContext.Request.Query.ContainsKey("pageSize"))
                {
                    method = string.Format(CultureInfo.InvariantCulture, $"{method}:pageSize:{pageSize}");
                }
            }

            return method;
        }
    }
}
