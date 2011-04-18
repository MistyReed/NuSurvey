﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuSurvey.Web.Controllers.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class UserAttribute : AuthorizeAttribute
    {
        public UserAttribute()
        {
            Roles = string.Format("{0},{1}", RoleNames.User, RoleNames.Admin);    //Set the roles prop to a comma delimited string of allowed roles
        }
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.Name.Contains("@"))
            {
                filterContext.HttpContext.Response.Redirect("~/Error");
            }
            base.OnAuthorization(filterContext);
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AdminAttribute : AuthorizeAttribute
    {
        public AdminAttribute()
        {
            Roles = RoleNames.Admin;    //Set the roles prop to a comma delimited string of allowed roles
        }
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.Name.Contains("@"))
            {
                filterContext.HttpContext.Response.Redirect("~/Error");
            }
            if (filterContext.HttpContext.User != null && !filterContext.HttpContext.User.IsInRole(RoleNames.Admin))
            {
                filterContext.HttpContext.Response.Redirect("~/Error/NotAuthorized");
            }
            base.OnAuthorization(filterContext);
        }
    }
    public class RoleNames
    {
        public static readonly string User = "NSUser";
        public static readonly string Admin = "NSAdmin";
    }
}