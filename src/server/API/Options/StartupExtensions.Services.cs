﻿// Copyright (c) 2020, UW Medicine Research IT, University of Washington
// Developed by Nic Dobbins and Cliff Spital, CRIO Sean Mooney
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
using API.Authentication;
using API.Authorization;
using API.Jobs;
using API.Jwt;
using API.Middleware.Federation;
using API.Middleware.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Model;
using Model.Admin.Compiler;
using Model.Admin.Network;
using Model.Admin.Query;
using Model.Admin.User;
using Model.Admin.Visualization;
using Model.Authentication;
using Model.Authorization;
using Model.Cohort;
using Model.Compiler;
using Model.Compiler.SqlServer;
using Model.Export;
using Model.Network;
using Model.Options;
using Model.Search;
using Model.Import;
using Model.Notification;
using Model.Obfuscation;
using Model.Visualization;
using Services.Admin.Compiler;
using Services.Admin.Network;
using Services.Admin.Query;
using Services.Admin.User;
using Services.Authentication;
using Services.Authorization;
using Services.Cohort;
using Services.Export;
using Services.Network;
using Services.Search;
using Services.Import;
using Services.Notification;
using Services.Obfuscation;

namespace API.Options
{
    public static partial class StartupExtensions
    {
        public static IServiceCollection RegisterLeafServices(
            this IServiceCollection services,
            Microsoft.AspNetCore.Hosting.IHostingEnvironment environment)
        {
            services.AddHttpContextAccessor();

            services.AddScoped<IUserContext, HttpUserContext>();
            services.AddTransient<UserContextLoggingMiddleware>();
            services.AddTransient<RejectInvalidFederatedUserMiddleware>();

            services.AddScoped<IServerContext, HttpServerContext>();

            services.AddHttpClient<IREDCapExportService, REDCapExportService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            });

            services.AddIAMServices();

            services.AddTransient<ISqlCompiler, SqlServerCompiler>();

            services.AddTransient<NetworkEndpointProvider.INetworkEndpointReader, NetworkEndpointReader>();

            services.AddNetworkCache();

            services.AddCohortQueryExecutionService();

            services.AddSingleton<NetworkEndpointConcurrentQueueSet>();

            services.AddTransient<IJwtKeyResolver, JwtKeyResolver>();

            services.AddHttpClient<INetworkEndpointRefresher, NetworkEndpointRefresher>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", @"application/json");
            });

            if (environment.IsProduction())
            {
                services.AddHostedService<BackgroundCertificateSynchronizer>();
            }

            services.AddTransient<ConceptHintSearcher.IConceptHintSearchService, ConceptHintSearchService>();
            services.AddTransient<ConceptTreeSearcher.IConceptTreeReader, ConceptTreeReader>();
            services.AddTransient<PreflightResourceChecker.IPreflightConceptReader, PreflightResourceReader>();
            services.AddTransient<PreflightResourceChecker.IPreflightResourceReader, PreflightResourceReader>();
            services.AddTransient<CohortCounter.ICohortCacheService, CohortCacheService>();
            services.AddTransient<IDemographicSqlCompiler, DemographicSqlCompiler>();
            services.AddTransient<DemographicCompilerValidationContextProvider.ICompilerContextProvider, DemographicCompilerContextProvider>();
            services.AddTransient<DemographicProvider.IDemographicsExecutor, DemographicsExecutor>();
            services.AddTransient<IDatasetSqlCompiler, DatasetSqlCompiler>();
            services.AddTransient<DatasetCompilerValidationContextProvider.ICompilerContextProvider, DatasetCompilerContextProvider>();
            services.AddTransient<IDatasetQueryFetcher, DatasetQueryFetcher>();
            services.AddTransient<DatasetProvider.IDatasetExecutor, DatasetExecutor>();
            services.AddTransient<IVisualizationPageFetcher, VisualizationPageFetcher>();
            services.AddTransient<IQueryService, QueryService>();
            services.AddTransient<DataImporter.IImportService, ImportService>();
            services.AddTransient<DataImporter.IImportIdentifierMappingService, ImportIdentifierMappingService>();
            services.AddTransient<NotificationManager.INotificationService, SmtpService>();
            services.AddTransient<IObfuscationService, ObfuscationService>();
            services.AddTransient<IConceptDatasetSqlCompiler, ConceptDatasetSqlCompiler>();
            services.AddTransient<ConceptDatasetCompilerValidationContextProvider.ICompilerContextProvider, ConceptDatasetCompilerContextProvider>();
            services.AddTransient<IPanelDatasetSqlCompiler, PanelDatasetSqlCompiler>();
            services.AddTransient<PanelDatasetCompilerValidationContextProvider.ICompilerContextProvider, PanelDatasetCompilerContextProvider>();

            services.AddAdminServices();
            services.RegisterLeafCore();

            return services;
        }

        static IServiceCollection AddAdminServices(this IServiceCollection services)
        {
            services.AddTransient<AdminConceptSqlSetManager.IAdminConceptSqlSetService, AdminConceptSqlSetService>();
            services.AddTransient<AdminSpecializationManager.IAdminSpecializationService, AdminSpecializationService>();
            services.AddTransient<AdminSpecializationGroupManager.IAdminSpecializationGroupService, AdminSpecializationGroupService>();
            services.AddTransient<AdminConceptManager.IAdminConceptService, AdminConceptService>();
            services.AddTransient<AdminConceptEventManager.IAdminConceptEventService, AdminConceptEventService>();
            services.AddTransient<AdminNetworkEndpointManager.IAdminNetworkUpdater, AdminNetworkEndpointUpdater>();
            services.AddTransient<AdminDatasetQueryManager.IAdminDatasetQueryService, AdminDatasetQueryService>();
            services.AddTransient<AdminVisualizationPageManager.IAdminVisualizationPageService, AdminVisualizationPageService>();
            services.AddTransient<AdminVisualizationCategoryManager.IAdminVisualizationCategoryService, AdminVisualizationCategoryService>();
            services.AddTransient<AdminDatasetCategoryManager.IAdminDatasetCategoryService, AdminDatasetCategoryService>();
            services.AddTransient<AdminDemographicsManager.IAdminDemographicQueryService, AdminDemographicQueryService>();
            services.AddTransient<AdminPanelFilterManager.IAdminPanelFilterService, AdminPanelFilterService>();
            services.AddTransient<AdminGlobalPanelFilterManager.IAdminGlobalPanelFilterService, AdminGlobalPanelFilterService>();
            services.AddTransient<AdminQueryManager.IAdminQueryService, AdminQueryService>();
            services.AddTransient<AdminUserManager.IAdminUserService, AdminUserService>();

            return services;
        }

        static IServiceCollection AddNetworkCache(this IServiceCollection services)
        {
            services.AddSingleton<INetworkEndpointCache, NetworkEndpointCache>(sp =>
            {
                var network = sp.GetService<NetworkEndpointProvider.INetworkEndpointReader>();
                var initial = network.GetEndpointsAsync().Result;

                return new NetworkEndpointCache(initial);
            });
            services.AddSingleton<INetworkResponderCacheReader, NetworkResponderCacheReader>();
            services.AddSingleton<INetworkInterrogatorCacheReader, NetworkInterrogatorCacheReader>();

            return services;
        }

        static IServiceCollection AddIAMServices(this IServiceCollection services)
        {
            services.AddSingleton<ITokenBlacklistCache, TokenBlacklistCache>();
            services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();
            services.AddHostedService<BackgroundTokenBlacklistSynchronizer>();

            var sp = services.BuildServiceProvider();
            var authenticationOptions = sp.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
            services.AddAuthenticationServices(authenticationOptions);

            var authorizationOptions = sp.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
            services.AddAuthorizationServices(authorizationOptions);

            services.AddTransient<IUserJwtProvider, JwtProvider>();
            services.AddTransient<IApiJwtProvider, JwtProvider>();
            services.AddTransient<ILoginSaver, LoginSaver>();

            return services;
        }

        static IServiceCollection AddCohortQueryExecutionService(this IServiceCollection services)
        {
            var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<IOptions<ClinDbOptions>>().Value;

            switch (opts.Cohort.QueryStrategy)
            {
                case ClinDbOptions.ClinDbCohortOptions.QueryStrategyOptions.CTE:
                    services.AddTransient<CohortCounter.IPatientCohortService, CtePatientCohortService>();
                    break;

                case ClinDbOptions.ClinDbCohortOptions.QueryStrategyOptions.Parallel:
                    services.AddTransient<CohortCounter.IPatientCohortService, ParallelPatientCohortService>();
                    break;
            }

            return services;
        }

        static IServiceCollection AddAuthenticationServices(this IServiceCollection services, AuthenticationOptions opts)
        {
            switch (opts.Mechanism)
            {
                case AuthenticationMechanism.Saml2:
                    services.AddScoped<IFederatedIdentityProvider, SAML2IdentityProvider>();
                    break;

                case AuthenticationMechanism.Unsecured:
                    services.AddSingleton<IFederatedIdentityProvider, UnsecureIdentityProvider>();
                    break;
            }

            return services;
        }

        static IServiceCollection AddAuthorizationServices(this IServiceCollection services, AuthorizationOptions opts)
        {
            switch (opts.Mechanism)
            {
                case AuthorizationMechanism.Saml2:
                    services.AddSingleton<IFederatedEntitlementProvider, SAML2EntitlementProvider>();
                    break;

                case AuthorizationMechanism.ActiveDirectory:
                    services.AddSingleton<IMembershipProvider, ActiveDirectoryMembershipProvider>();
                    services.AddScoped<IFederatedEntitlementProvider, ActiveDirectoryEntitlementProvider>();
                    break;

                case AuthorizationMechanism.Unsecured:
                    services.AddSingleton<IFederatedEntitlementProvider, UnsecureEntitlementProvider>();
                    break;
            }

            return services;
        }
    }
}
