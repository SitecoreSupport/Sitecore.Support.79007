namespace Sitecore.Support.EmailCampaign.Server.Filters
{
    using Sitecore.Diagnostics;
    using Sitecore.Security.Accounts;
    using System;
    using System.Web.Http;
    using System.Web.Http.Controllers;

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    internal sealed class SitecoreAuthorizeAttribute : AuthorizeAttribute
    {
        private static readonly ITicketManager TicketManager = new TicketManagerWrapper();

        public SitecoreAuthorizeAttribute(params string[] roles)
        {
            base.Roles = string.Join(",", roles);
        }

        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            Assert.ArgumentNotNull(actionContext, "actionContext");
            bool flag = base.IsAuthorized(actionContext) && !this.AdminsOnly;
            User principal = actionContext.ControllerContext.RequestContext.Principal as User;
            bool flag2 = (principal != null) && principal.IsAdministrator;
            return ((flag || flag2) && TicketManager.IsCurrentTicketValid());
        }

        public bool AdminsOnly { get; set; }

        internal interface ITicketManager
        {
            bool IsCurrentTicketValid();
        }

        private class TicketManagerWrapper : SitecoreAuthorizeAttribute.ITicketManager
        {
            public bool IsCurrentTicketValid() =>
                Sitecore.Web.Authentication.TicketManager.IsCurrentTicketValid();
        }
    }
}
