﻿using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CSE.Helium.Validation
{
    public class ValidationProblemDetails : ProblemDetails
    {
        private readonly List<ValidationError> validationErrors = new List<ValidationError>();

        [JsonPropertyName("validationErrors")]
        public ICollection<ValidationError> ValidationErrors => validationErrors;
    }
}
