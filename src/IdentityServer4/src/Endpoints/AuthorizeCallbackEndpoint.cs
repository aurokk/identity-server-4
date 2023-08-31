// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using IdentityServer4.Configuration;
using IdentityServer4.Endpoints.Results;
using IdentityServer4.Extensions;
using IdentityServer4.Hosting;
using IdentityServer4.Models;
using IdentityServer4.ResponseHandling;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IdentityServer4.Endpoints
{
    internal class AuthorizeCallbackEndpoint : AuthorizeEndpointBase
    {
        private readonly ILoginMessageStore _loginMessageStore;
        private readonly IConsentMessageStore _consentResponseStore;
        private readonly IAuthorizationParametersMessageStore _authorizationParametersMessageStore;

        public AuthorizeCallbackEndpoint(
            IEventService events,
            ILogger<AuthorizeCallbackEndpoint> logger,
            IdentityServerOptions options,
            IAuthorizeRequestValidator validator,
            IAuthorizeInteractionResponseGenerator interactionGenerator,
            IAuthorizeResponseGenerator authorizeResponseGenerator,
            IUserSession userSession,
            IConsentMessageStore consentResponseStore,
            ILoginMessageStore loginMessageStore,
            IAuthorizationParametersMessageStore authorizationParametersMessageStore = null)
            : base(events, logger, options, validator, interactionGenerator, authorizeResponseGenerator, userSession)
        {
            _consentResponseStore = consentResponseStore;
            _loginMessageStore = loginMessageStore;
            _authorizationParametersMessageStore = authorizationParametersMessageStore;
        }

        public override async Task<IEndpointResult> ProcessAsync(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                Logger.LogWarning("Invalid HTTP method for authorize endpoint.");
                return new StatusCodeResult(HttpStatusCode.MethodNotAllowed);
            }

            Logger.LogDebug("Start authorize callback request");

            var parameters = context.Request.Query.AsNameValueCollection();
            if (_authorizationParametersMessageStore != null)
            {
                // TODO: поисследовать че за мессадж стор
                var messageStoreId = parameters[Constants.AuthorizationParamsStore.MessageStoreIdParameterName];
                var entry = await _authorizationParametersMessageStore.ReadAsync(messageStoreId);
                parameters = entry?.Data.FromFullDictionary() ?? new NameValueCollection();

                await _authorizationParametersMessageStore.DeleteAsync(messageStoreId);
            }

            var loginRequestId = Guid.NewGuid().ToString("N");
            var loginRequest = new LoginRequest(parameters, loginRequestId);
            var loginResult = await _loginMessageStore.ReadAsync(loginRequest.Id);
            if (loginResult is { Data: null })
            {
                return await CreateErrorResultAsync("login message is missing data");
            }

            // TODO:
            // – тут нужно авторизовать пользователя и создать куку, если есть подтверждение от IdP, что всё ок, если не ок, то можно кинуть ошибку
            //   так же как в AuthorizeEndpoint
            var user = await UserSession.GetUserAsync();

            // TODO:
            // – на куках не очень удобно, нужны редиректы, чтоб прописать куки
            var consentRequest = new ConsentRequest(parameters, user?.GetSubjectId());
            var consentResult = await _consentResponseStore.ReadAsync(consentRequest.Id);
            if (consentResult is { Data: null })
            {
                return await CreateErrorResultAsync("consent message is missing data");
            }

            try
            {
                var result = await ProcessAuthorizeRequestAsync(parameters, user, consentResult?.Data);

                Logger.LogTrace("End Authorize Request. Result type: {0}", result?.GetType().ToString() ?? "-none-");

                return result;
            }
            finally
            {
                if (consentResult != null)
                {
                    await _consentResponseStore.DeleteAsync(consentRequest.Id);
                }
            }
        }
    }
}