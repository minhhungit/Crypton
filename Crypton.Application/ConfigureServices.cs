﻿using System.Reflection;
using Crypton.Application.Common.Behaviours;
using Crypton.Application.Interfaces;
using Crypton.Application.Services;
using Crypton.Domain;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Crypton.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(DomainAssembly.Assembly);
        services.AddValidatorsFromAssembly(ApplicationAssembly.Assembly);

        services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(ApplicationAssembly.Assembly); });
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            // if the request is deemed invalid, we will not allow it from even executing
            // this is why it is added first
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequestValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorOrBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehaviour<,>));
        });

        services.AddSingleton<ITransactionService, TransactionService>(sp => new TransactionService(sp));

        return services;
    }
}