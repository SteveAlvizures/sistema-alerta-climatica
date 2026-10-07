using ClimateAlert.Application.Common.Interfaces;
using ClimateAlert.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClimateAlert.Api.Audit;

public sealed class AtomicAdministrativeOperationFilter(ClimateAlertDbContext database, IUnitOfWork unitOfWork)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        try
        {
            ActionExecutedContext executed;
            using (database.DeferSaveChanges()) executed = await next();

            if (executed.Exception is not null || executed.Canceled
                || executed.Result is ObjectResult { StatusCode: >= 400 }
                || executed.Result is StatusCodeResult { StatusCode: >= 400 })
            {
                database.ChangeTracker.Clear();
                return;
            }

            // One SaveChanges: all business mutations and every audit action commit
            // together. A SQL failure rolls back the entire EF transaction.
            await unitOfWork.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
        catch
        {
            database.ChangeTracker.Clear();
            throw;
        }
    }
}
